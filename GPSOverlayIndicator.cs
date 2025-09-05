// GPSOverlayIndicator.cs - improved version
// This indicator displays signals from the GPS backend on the chart.
// Added customizable API endpoint and better thread control.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using System.Runtime.InteropServices;

namespace NinjaTrader.NinjaScript.Indicators
{
    public class GPSOverlayIndicator : Indicator
    {
        [DllImport("winmm.dll", SetLastError = true)]
        static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

        private HttpClient httpClient;
        private System.Threading.Timer signalCheckTimer;
        private List<GPSSignal> activeSignals = new List<GPSSignal>();
        private DateTime lastSignalTime = DateTime.MinValue;
        private CancellationTokenSource cancellationTokenSource;
        private readonly object lockObject = new object();
        private volatile int isCheckingSignals;

        [NinjaScriptProperty]
        [Display(Name = "Intervalle vérification (ms)", Order = 1, GroupName = "Configuration")]
        public int PollInterval { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Afficher TP/SL", Order = 2, GroupName = "Configuration")]
        public bool ShowTPSL { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "API URL", Order = 3, GroupName = "Configuration")]
        public string ApiUrl { get; set; }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "GPS Overlay - Affiche graphiquement les signaux du Cerveau IA";
                Name = "GPS Overlay Indicator";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                DrawOnPricePanel = true;
                IsSuspendedWhileInactive = false;
                PollInterval = 3000;
                ShowTPSL = true;
                ApiUrl = "http://192.168.64.1:8000/next-signal";
            }
            else if (State == State.Configure)
            {
                httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                cancellationTokenSource = new CancellationTokenSource();
            }
            else if (State == State.DataLoaded)
            {
                signalCheckTimer = new System.Threading.Timer(CheckForSignals, null, 1000, PollInterval);
            }
            else if (State == State.Terminated)
            {
                try
                {
                    cancellationTokenSource?.Cancel();
                    signalCheckTimer?.Dispose();
                    httpClient?.Dispose();
                    cancellationTokenSource?.Dispose();
                }
                catch { }
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 1) return;
            lock (lockObject)
            {
                for (int i = activeSignals.Count - 1; i >= 0; i--)
                {
                    if ((DateTime.Now - activeSignals[i].timestamp).TotalMinutes > 60)
                        activeSignals.RemoveAt(i);
                }
            }
        }

        private async void CheckForSignals(object state)
        {
            if (Interlocked.Exchange(ref isCheckingSignals, 1) == 1)
                return;

            try
            {
                using (var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, timeoutTokenSource.Token))
                {
                    var response = await httpClient.GetAsync(ApiUrl, cts.Token);
                    if (!response.IsSuccessStatusCode)
                        return;

                    var json = await response.Content.ReadAsStringAsync();
                    var signalResponse = ParseSignalResponse(json);
                    if (signalResponse?.signal != null && signalResponse.signal.timestamp > lastSignalTime)
                    {
                        lock (lockObject)
                        {
                            lastSignalTime = signalResponse.signal.timestamp;
                            activeSignals.Add(signalResponse.signal);
                            if (activeSignals.Count > 20)
                                activeSignals.RemoveAt(0);
                        }
                        if (ChartControl?.Dispatcher != null)
                            ChartControl.Dispatcher.Invoke(() => DrawSignalOnChart(signalResponse.signal));
                        else
                            DrawSignalOnChart(signalResponse.signal);
                    }
                }
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref isCheckingSignals, 0);
            }
        }

        private SignalResponse ParseSignalResponse(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"signal\""))
                    return null;

                var signal = new GPSSignal
                {
                    id = ExtractJsonString(json, "id"),
                    symbol = ExtractJsonString(json, "symbol"),
                    side = ExtractJsonString(json, "side"),
                    entry_price = ExtractJsonDouble(json, "entry_price"),
                    size = ExtractJsonInt(json, "size"),
                    take_profit = ExtractJsonNullableDouble(json, "take_profit"),
                    stop_loss = ExtractJsonNullableDouble(json, "stop_loss"),
                    confidence = ExtractJsonDouble(json, "confidence"),
                    market_condition = ExtractJsonString(json, "market_condition")
                };

                var ts = ExtractJsonString(json, "timestamp");
                if (DateTime.TryParse(ts, out var t))
                    signal.timestamp = t;
                else
                    signal.timestamp = DateTime.Now;

                return new SignalResponse { signal = signal, message = ExtractJsonString(json, "message") };
            }
            catch
            {
                return null;
            }
        }

        private string ExtractJsonString(string json, string key)
        {
            var pattern = $"\"{key}\"\\s*:\\s*\"([^\"]+)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private double ExtractJsonDouble(string json, string key)
        {
            var pattern = $"\"{key}\"\\s*:\\s*([0-9.\\-]+)";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return v;
            return 0;
        }

        private double? ExtractJsonNullableDouble(string json, string key)
        {
            var pattern = $"\"{key}\"\\s*:\\s*([0-9.\\-]+|null)";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            if (match.Success && match.Groups[1].Value != "null" && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return v;
            return null;
        }

        private int ExtractJsonInt(string json, string key)
        {
            var pattern = $"\"{key}\"\\s*:\\s*([0-9]+)";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var v))
                return v;
            return 0;
        }

        private void DrawSignalOnChart(GPSSignal signal)
        {
            try
            {
                var timestamp = signal.timestamp.ToString("HHmmssfff");
                var isLong = signal.side.ToUpper() == "LONG" || signal.side.ToUpper() == "BUY";
                double currentPrice = Close[0];
                double highPrice = High[0];
                double lowPrice = Low[0];
                double entryPrice = signal.entry_price;

                if (isLong)
                {
                    var arrowPrice = lowPrice - (TickSize * 3);
                    Draw.ArrowUp(this, "GPSLong" + timestamp, true, 0, arrowPrice, Brushes.Lime);
                    Draw.Text(this, "GPSLongText" + timestamp,
                        $"GPS {signal.symbol} LONG\nConfiance: {signal.confidence:P0}\nSignal: {entryPrice:F2}",
                        0, arrowPrice - (TickSize * 12), Brushes.LimeGreen);
                }
                else
                {
                    var arrowPrice = highPrice + (TickSize * 3);
                    Draw.ArrowDown(this, "GPSShort" + timestamp, true, 0, arrowPrice, Brushes.Red);
                    Draw.Text(this, "GPSShortText" + timestamp,
                        $"GPS {signal.symbol} SHORT\nConfiance: {signal.confidence:P0}\nSignal: {entryPrice:F2}",
                        0, arrowPrice + (TickSize * 12), Brushes.OrangeRed);
                }

                Draw.HorizontalLine(this, "GPSEntry" + timestamp, currentPrice,
                    isLong ? Brushes.LimeGreen : Brushes.OrangeRed, DashStyleHelper.Solid, 3);

                if (ShowTPSL && signal.take_profit.HasValue && signal.stop_loss.HasValue)
                {
                    var range = Math.Abs(signal.take_profit.Value - signal.entry_price);
                    var slRange = Math.Abs(signal.entry_price - signal.stop_loss.Value);
                    if (isLong)
                    {
                        var adjustedTP = currentPrice + range;
                        var adjustedSL = currentPrice - slRange;
                        Draw.HorizontalLine(this, "GPSTP" + timestamp, adjustedTP, Brushes.Green, DashStyleHelper.Dash, 2);
                        Draw.Text(this, "GPSTPText" + timestamp, $"TP: {adjustedTP:F2}", 3, adjustedTP, Brushes.Green);
                        Draw.HorizontalLine(this, "GPSSL" + timestamp, adjustedSL, Brushes.Red, DashStyleHelper.Dash, 2);
                        Draw.Text(this, "GPSSLText" + timestamp, $"SL: {adjustedSL:F2}", 3, adjustedSL, Brushes.Red);
                    }
                    else
                    {
                        var adjustedTP = currentPrice - range;
                        var adjustedSL = currentPrice + slRange;
                        Draw.HorizontalLine(this, "GPSTP" + timestamp, adjustedTP, Brushes.Green, DashStyleHelper.Dash, 2);
                        Draw.Text(this, "GPSTPText" + timestamp, $"TP: {adjustedTP:F2}", 3, adjustedTP, Brushes.Green);
                        Draw.HorizontalLine(this, "GPSSL" + timestamp, adjustedSL, Brushes.Red, DashStyleHelper.Dash, 2);
                        Draw.Text(this, "GPSSLText" + timestamp, $"SL: {adjustedSL:F2}", 3, adjustedSL, Brushes.Red);
                    }
                }

                double zoneTop = highPrice + TickSize;
                double zoneBottom = lowPrice - TickSize;
                Draw.Rectangle(this, "GPSZone" + timestamp, true, 1, zoneTop, -1, zoneBottom,
                    isLong ? Brushes.LimeGreen : Brushes.OrangeRed,
                    isLong ? Brushes.LimeGreen : Brushes.OrangeRed, 30);
                Draw.VerticalLine(this, "GPSTime" + timestamp, Time[0],
                    isLong ? Brushes.LimeGreen : Brushes.OrangeRed, DashStyleHelper.Dot, 2);
                Draw.Dot(this, "GPSDot" + timestamp, true, 0, currentPrice,
                    isLong ? Brushes.Lime : Brushes.Red);

                try { PlaySound(null, IntPtr.Zero, 0x0040); } catch { }
            }
            catch { }
        }

        #region Properties
        [Browsable(false)]
        public Series<double> LongSignal => Values[0];

        [Browsable(false)]
        public Series<double> ShortSignal => Values[1];
        #endregion
    }

    public class SignalResponse
    {
        public GPSSignal signal { get; set; }
        public string message { get; set; }
    }

    public class GPSSignal
    {
        public string id { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public double entry_price { get; set; }
        public int size { get; set; }
        public double? take_profit { get; set; }
        public double? stop_loss { get; set; }
        public double confidence { get; set; }
        public DateTime timestamp { get; set; }
        public string market_condition { get; set; }
    }
}

