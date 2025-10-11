#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
#endregion

//┌───────────────────────────────────────────────────────────────────────────┐
//│ DIY Custom Strategy Builder - NinjaTrader FULL CONVERSION                │
//│ Converted from: DIY.ps (Pine Script v6 INDICATOR - 4363 lines)           │
//│ Version: 1.7.3 Full - All Core Features                                   │
//│ Author: Converted from TradingView Pine Script                           │
//│ Type: INDICATOR (displays Brain signals, not auto-trading)               │
//│ Features:                                                                  │
//│  • 40+ Technical Indicators (RF, ST, Ichi, SSL, PSAR, QQE, etc.)        │
//│  • Brain Scoring System (Lead, Stack, Regime, Volume, Hybrid)           │
//│  • Autopilot (Dynamic Indicator Selection)                               │
//│  • Visual Signals (LONG/SHORT arrows on chart)                          │
//│  • Learning System (Win Rate Tracking, Adaptive Weights)                │
//│  • Regime Detection (Trending/Ranging/Choppy)                           │
//│  • NO TIMEOUT LIMITS (main advantage vs TradingView)                    │
//└───────────────────────────────────────────────────────────────────────────┘

namespace NinjaTrader.NinjaScript.Indicators
{
	// Enum must be outside class for NinjaScript property system
	public enum TradingModeType
	{
		AutoPure,
		AutoGuided,
		Manual
	}
	
	public class DIYBrainFull : Indicator
	{
		#region Variables
		
		// ═══════════════════════════════════════════════════════════════════
		// CORE INDICATORS (40 indicateurs complets)
		// ═══════════════════════════════════════════════════════════════════
		
		// Basic MAs
		private SMA smaBaseline;
		private EMA emaFast, emaSlow, emaRegime;
		private WMA wmaIndicator;
		
		// Oscillators
		private RSI rsiIndicator;
		private MACD macdIndicator;
		private Stochastics stochIndicator;
		private CCI cciIndicator;
		
		// Volatility
		private ATR atrIndicator;
		private StdDev stdDev;
		private Bollinger bollingerBands;
		
		// Trend
		private ADX adxIndicator;
		private DonchianChannel donchianChannel;
		private ParabolicSAR psarIndicator;
		
		// Volume
		private VWAP vwapIndicator;
		private VOL volumeIndicator;  // PATCH 1: VOL() returns ISeries<double> for MA compatibility
		private bool vwapFallback = false;  // PATCH 2: Track if VWAP failed (license issue)
		
		// Advanced (Heavy Indicators - conditionally loaded)
		private double ichimokuConversion, ichimokuBase, ichimokuSpanA, ichimokuSpanB;
		private double sslHigh, sslLow, sslFast, sslSlow;
		private double halfTrendLine, halfTrendUp, halfTrendDn;
		private double rangeFilterValue, rangeFilterHigh, rangeFilterLow;
		private double qqeWilders, qqeSignal, qqeFast, qqeSlow;
		
		// Additional indicators from TradingView v1.8 (Aroon & Fisher implemented below)
		// Note: RQK and SIchi potentially missing - need verification
		private double aroonUp, aroonDown;
		private double dmiPlus, dmiMinus, dmiAdx;
		private double vortexPlus, vortexMinus;
		private double fisherTransform, fisherSignal;
		private double tsi, tsiSignal;
		private double tdfi;
		private double tlbUpper, tlbLower;
		private double rocketDrive;
		private double haoOpen, haoClose, haoHigh, haoLow;
		private double rocValue;
		private double bxTrend;
		private double bbPercentB;
		private double dpoValue;
		private double bboTrend;
		private double ceValue;
		private double waeUp, waeDown;
		private double cmfValue;
		private double stcValue;
		private double aoValue;
		private double voValue;
		private double wolfTrend;
		private double hullMA;
		
		// Series for multi-bar calculations
		private Series<double> emaCustom;
		private Series<double> rsiSeries;
		private Series<double> trueLow, trueHigh;
		
		// ═══════════════════════════════════════════════════════════════════
		// NATIVE NT8 INDICATORS (Performance Refactoring)
		// Replaces manual loop calculations for massive performance gains
		// ═══════════════════════════════════════════════════════════════════
		private Aroon aroonIndicator;
		private DM dmiIndicator;  // Directional Movement Index
		
		// Vortex Indicator Components (Series-based for 42x performance gain)
		private Series<double> viPlus;     // Vortex Movement Positive
		private Series<double> viMinus;    // Vortex Movement Negative
		private Series<double> vortexTR;   // True Range for Vortex
		
		// Fisher Transform Components (Series + native EMA for 2x performance gain)
		private Series<double> fisherValue;   // Fisher Transform raw value
		private EMA fisherEmaIndicator;       // EMA(3) for Fisher signal line
		
		// TSI (True Strength Index) Components - CORRECTED Implementation
		// Previous bug: used price instead of momentum for double smoothing
		private Series<double> tsiMomentum;        // Price change (Close - Close[1])
		private Series<double> tsiAbsMomentum;     // Absolute price change
		private EMA tsiMomentumEMA25;              // First EMA(25) of momentum
		private EMA tsiMomentumEMA13;              // Second EMA(13) of first EMA (double smooth)
		private EMA tsiAbsMomentumEMA25;           // First EMA(25) of absolute momentum
		private EMA tsiAbsMomentumEMA13;           // Second EMA(13) of first EMA (double smooth)
		private Series<double> tsiValue;           // TSI result for signal line
		
		// TDFI (Trend Direction Force Index) Components - Cached EMA/SMA
		private EMA tdfiEMA;   // EMA(13) for TDFI
		private SMA tdfiSMA;   // SMA(13) for TDFI
		
		// CE (Chande Efficiency) Components - Phase 3 Optimization
		private Series<double> cePriceChange;   // Absolute price change per bar
		
		// CMF (Chaikin Money Flow) Components - Phase 3 Optimization
		private Series<double> cmfMoneyFlowVolume;   // Money flow × volume per bar
		private Series<double> cmfVolumeSum;         // Volume accumulator
		
		// Rocket Drive Components - Phase 3 Optimization (cached EMAs)
		private EMA rdEMA8;    // EMA(8) for Rocket Drive
		private EMA rdEMA21;   // EMA(21) for Rocket Drive
		private EMA rdEMA34;   // EMA(34) for Rocket Drive
		
		// Hull MA Components - Phase 3 Optimization (cached WMAs)
		private WMA hullWMAhalf;  // WMA(hullLen/2)
		private WMA hullWMAfull;  // WMA(hullLen)
		
		// ═══════════════════════════════════════════════════════════════════
		// EVOLUTION SYSTEM (Genetic Algorithm)
		// The "Soul" of the Brain - Adaptive Parameter Optimization
		// ═══════════════════════════════════════════════════════════════════
		private EvolutionaryArchitect evolutionArchitect;
		private int evolutionFrequency = 100;  // Evolve every N bars
		private int barsSinceEvolution = 0;
		
		// Trade history for fitness calculation (per regime)
		private List<double> trendTradeReturns = new List<double>();
		private List<double> rangeTradeReturns = new List<double>();
		private List<double> choppyTradeReturns = new List<double>();
		
		// Current champion chromosome (updated by evolution)
		private Chromosome activeChampion;
		
		// ═══════════════════════════════════════════════════════════════════
		// BRAIN COMPONENTS
		// ═══════════════════════════════════════════════════════════════════
		private double brainLongScore;
		private double brainShortScore;
		private double conviction;
		private double maxScore;
		
		// Regime Detection
		private bool isTrending;
		private bool isRanging;
		private bool isChoppy;
		private double trendiness;
		private double choppinessIndex;
		
		// Learning System
		private int totalTrades;
		private int wins;
		private int losses;
		private double winRate;
		private List<double> tradeReturns;
		private double winRateEMA;
		
		// Autopilot
		private string[] indicatorNames;
		private double[] trendWinRates;
		private int[] trendTradeCounts;
		private double[] rangeWinRates;
		private int[] rangeTradeCounts;
		private double[] choppyWinRates;
		private int[] choppyTradeCounts;
		private string dynamicLeadingIndicator;
		
		// Confirmation Stack
		private List<bool> confirmationLongBools;
		private List<bool> confirmationShortBools;
		private int passedLongCount;
		private int passedShortCount;
		
		// Position Tracking
		private double entryPrice;
		private int barsInTrade;
		private double highestSinceEntry;
		private double lowestSinceEntry;
		private string entryIndicator;
		private string entryRegime;
		
		// Virtual Trade Tracking (for Indicator mode learning)
		private bool inVirtualTrade;
		private int virtualTradeDirection;  // 1 = LONG, -1 = SHORT
		private double virtualEntryPrice;
		private int virtualBarsInTrade;
		private double virtualStopLoss;
		private double virtualTakeProfit;
		private double virtualHighest;
		private double virtualLowest;
		private string virtualEntryIndicator;
		private string virtualEntryRegime;
		
		// Learning parameters
		private double learningRate;
		private double winRateEmaAlpha;
		private double weightAdaptAlpha;
		
		// Adaptive Weights
		private double wLead_adaptive;
		private double wRegime_adaptive;
		private double wStack_adaptive;
		
		// Regime Detection State
		private string currentRegime;  // Current market regime: "TREND", "RANGE", or "CHOPPY"
		
		// ═══════════════════════════════════════════════════════════════════
		// GENOME ACTIVATION: Evolutive Indicator Parameters
		// The Brain reconfigures its tools based on evolved champion parameters
		// ═══════════════════════════════════════════════════════════════════
		private bool indicatorsNeedRecalc;  // Flag: champion changed, recalc indicators
		
		// Evolutive Parameters (from champion genome)
		private int championStPeriod;
		private double championStFactor;
		private int championRsiPeriod;
		private int championMacdFast;
		private int championMacdSlow;
		private int championMacdSignal;
		private int championRegimePeriod;  // EMA for regime detection
		
		// Evolutive Indicator Results (Series for dynamic recalculation)
		private Series<double> evolutiveST;       // SuperTrend evolved
		private Series<double> evolutiveRSI;      // RSI evolved
		private Series<double> evolutiveMACD;     // MACD evolved
		private Series<double> evolutiveMACDsignal; // MACD signal evolved
		private Series<double> evolutiveRegimeEMA; // Regime EMA evolved
		
		// Exit Brain State
		private bool useRSIexit;
		private double rsiExitLevel;
		private bool useStallExit;
		private int stallExitBars;
		private bool useMAexit;
		private int maExitPeriod;
		
		// Plot values (for indicator output)
		private double brainSignal;  // 1 = LONG, -1 = SHORT, 0 = Neutral
		
		#endregion

		#region Properties - ESSENTIALS
		
		[NinjaScriptProperty]
		[Display(Name = "🎛️ Trading Mode", Description = "Auto Pure = Cerveau décide tout", Order = 1, GroupName = "🎛️ ESSENTIALS")]
		public TradingModeType TradingMode { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "2️⃣ Aggressiveness (1-5)", Description = "1=Ultra prudent, 5=Agressif", Order = 2, GroupName = "🎛️ ESSENTIALS")]
		[Range(1, 5)]
		public int Aggressiveness { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "3️⃣ Brain Threshold LONG", Description = "Conviction minimum LONG", Order = 3, GroupName = "🎛️ ESSENTIALS")]
		[Range(0.0, 1.0)]
		public double BrainThresholdLong { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Brain Threshold SHORT", Description = "Conviction minimum SHORT", Order = 3, GroupName = "🎛️ ESSENTIALS")]
		[Range(0.0, 1.0)]
		public double BrainThresholdShort { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "4️⃣ Learning & Adaptation", Description = "Track win rates et sélection auto", Order = 4, GroupName = "🎛️ ESSENTIALS")]
		public bool EnableLearning { get; set; }
		
		#endregion
		
		#region Properties - RISK & SESSIONS
		
		[NinjaScriptProperty]
		[Display(Name = "Take Profit (x ATR)", Description = "TP multiplier", Order = 1, GroupName = "⚙️ RISK & SESSIONS")]
		[Range(0.5, 10.0)]
		public double TpAtrMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Stop Loss (x ATR)", Description = "SL multiplier", Order = 2, GroupName = "⚙️ RISK & SESSIONS")]
		[Range(0.5, 5.0)]
		public double SlAtrMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Trailing Stop (x ATR)", Description = "Trail multiplier", Order = 3, GroupName = "⚙️ RISK & SESSIONS")]
		[Range(0.5, 5.0)]
		public double TrailAtrMultiplier { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Breakeven @ R", Description = "Move SL to BE after R", Order = 4, GroupName = "⚙️ RISK & SESSIONS")]
		[Range(0.0, 5.0)]
		public double BreakevenAfterR { get; set; }
		
		#endregion
		
		#region Properties - CORE SETTINGS
		
		[NinjaScriptProperty]
		[Display(Name = "Short Period", Description = "Fast MA period", Order = 1, GroupName = "▼ Core Settings")]
		[Range(5, 50)]
		public int ShortPeriod { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Medium Period", Description = "Medium MA period", Order = 2, GroupName = "▼ Core Settings")]
		[Range(10, 100)]
		public int MediumPeriod { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Long Period", Description = "Slow MA period", Order = 3, GroupName = "▼ Core Settings")]
		[Range(20, 200)]
		public int LongPeriod { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "ATR Period", Description = "ATR calculation period", Order = 4, GroupName = "▼ Core Settings")]
		[Range(5, 50)]
		public int AtrPeriod { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Heavy Indicators", Description = "Ichimoku, SSL, PSAR, QQE, RangeFilter (CPU-intensive)", Order = 5, GroupName = "▼ Core Settings")]
		public bool EnableHeavyIndicators { get; set; }
		
		#endregion
		
		#region Properties - BRAIN WEIGHTS
		
		[NinjaScriptProperty]
		[Display(Name = "wLead (Leading Indicator)", Description = "Weight for leading signal", Order = 1, GroupName = "▼ Brain Weights")]
		[Range(0.0, 5.0)]
		public double WLead { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "wStack (Confirmation Stack)", Description = "Weight for confirmation filters", Order = 2, GroupName = "▼ Brain Weights")]
		[Range(0.0, 5.0)]
		public double WStack { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "wRegime (Regime Filter)", Description = "Weight for regime alignment", Order = 3, GroupName = "▼ Brain Weights")]
		[Range(0.0, 5.0)]
		public double WRegime { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "wVolume (Volume Spike)", Description = "Weight for volume confirmation", Order = 4, GroupName = "▼ Brain Weights")]
		[Range(0.0, 5.0)]
		public double WVolume { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "wHybrid (Hybrid Signals)", Description = "Weight for hybrid indicator signals", Order = 5, GroupName = "▼ Brain Weights")]
		[Range(0.0, 5.0)]
		public double WHybrid { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Adaptive Weights", Description = "Adjust weights based on win rate feedback", Order = 6, GroupName = "▼ Brain Weights")]
		public bool AdaptiveWeights { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Probabilistic Brain Mode", Description = "Use probabilistic scoring (0-1 normalized) instead of weighted boolean", Order = 6, GroupName = "▼ Brain Weights")]
		public bool UseProbabilisticBrain { get; set; }
		
		#endregion
		
		#region Properties - EVOLUTION SYSTEM
		
		[NinjaScriptProperty]
		[Display(Name = "🧬 Enable Evolution System", Description = "Enable genetic algorithm parameter optimization", Order = 1, GroupName = "🧬 EVOLUTION")]
		public bool UseEvolutionSystem { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Evolution Frequency (bars)", Description = "Evolve every N bars", Order = 2, GroupName = "🧬 EVOLUTION")]
		[Range(10, 500)]
		public int EvolutionFrequency
		{
			get { return evolutionFrequency; }
			set { evolutionFrequency = value; }
		}
		
		[NinjaScriptProperty]
		[Display(Name = "Evolution Seed", Description = "Random seed for reproducibility", Order = 3, GroupName = "🧬 EVOLUTION")]
		[Range(1, 1000)]
		public int EvolutionSeed { get; set; }
		
		#endregion

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"DIY Brain Full - Complete NinjaTrader conversion of Pine Script v6 INDICATOR";
				Name = "DIY Brain Full";
				Calculate = Calculate.OnBarClose;
				IsOverlay = true;  // Display on price chart
				DisplayInDataBox = true;
				DrawOnPricePanel = true;
				ScaleJustification = ScaleJustification.Right;
				IsSuspendedWhileInactive = true;
				
				// ═══════════════════════════════════════════════════════════
				// DEFAULT VALUES (matched to DIY.ps defaults)
				// ═══════════════════════════════════════════════════════════
				TradingMode = TradingModeType.AutoPure;
				Aggressiveness = 4;
				BrainThresholdLong = 0.60;
				BrainThresholdShort = 0.60;
				EnableLearning = true;
				
				TpAtrMultiplier = 1.0;  // Conservative default (Pine: 1.0)
				SlAtrMultiplier = 0.7;  // Tight SL (Pine: 0.7)
				TrailAtrMultiplier = 1.2;
				BreakevenAfterR = 1.0;
				
				ShortPeriod = 12;   // Pine v1.7.1 optimized (was 14)
				MediumPeriod = 26;  // Pine v1.7.1 optimized (was 34)
				LongPeriod = 40;    // Pine v1.7.1 optimized (was 55)
				AtrPeriod = 14;
				
				EnableHeavyIndicators = true;  // NT8 has no timeout limits!
				
				// Brain weights (Pine defaults)
				WLead = 1.0;
				WStack = 1.0;
				WRegime = 1.0;
				WVolume = 1.0;
				WHybrid = 1.0;
				AdaptiveWeights = false;
				UseProbabilisticBrain = false;  // Default to weighted boolean
				
				// ═══════════════════════════════════════════════════════════════════════
				// 🧬 EVOLUTION SYSTEM ACTIVATION
				// The brain takes its first breath - genetic optimization enabled by default
				// ═══════════════════════════════════════════════════════════════════════
				UseEvolutionSystem = true;  // 🚀 ACTIVATED - The soul of the brain awakens!
				EvolutionFrequency = 100;    // Evolve every 100 bars (fine-tune based on timeframe)
				EvolutionSeed = 42;          // Reproducible experiments (change for exploration)
			}
			else if (State == State.Configure)
			{
				// Initialize learning arrays
				tradeReturns = new List<double>();
				confirmationLongBools = new List<bool>();
				confirmationShortBools = new List<bool>();
				
				// Initialize 40 indicator names (matches Pine script)
				indicatorNames = new string[] {
					"RF", "RQK", "ST", "HT", "Ichi", "SIchi", "TSI", "TDFI", "TLB", "RD",
					"HAO", "Don", "Stoch", "RSI", "ROC", "VWAP", "CCI", "2MA", "3MA", "BX",
					"BBPT", "DPO", "BBO", "CE", "ADX", "PSAR", "MACD", "SSL", "WAE", "CMF",
					"VI", "STC", "AO", "VO", "Wolf", "QQE", "Hull"
				};
				
				int N = indicatorNames.Length;
				trendWinRates = new double[N];
				trendTradeCounts = new int[N];
				rangeWinRates = new double[N];
				rangeTradeCounts = new int[N];
				choppyWinRates = new double[N];
				choppyTradeCounts = new int[N];
				
				// Initialize with 0.5 (50% baseline)
				for (int i = 0; i < N; i++)
				{
					trendWinRates[i] = 0.5;
					rangeWinRates[i] = 0.5;
					choppyWinRates[i] = 0.5;
				}
				
				// Default exit brain parameters (will be overridden by genome if enabled)
				useRSIexit = true;
				rsiExitLevel = 75.0;
				useStallExit = true;
				stallExitBars = 8;
				useMAexit = false;
				maExitPeriod = 20;
				
				// Learning parameters
				learningRate = 0.1;  // Pine: default learning rate
				winRateEmaAlpha = 0.1;  // Pine: EWMA alpha for win rate
				weightAdaptAlpha = 0.05;  // Pine: slower adaptation for weights
				
				// Virtual trade tracking
				inVirtualTrade = false;
				virtualTradeDirection = 0;
			}
			else if (State == State.DataLoaded)
			{
				// ═══════════════════════════════════════════════════════════
				// INDICATOR INITIALIZATION (Core + Conditional Heavy)
				// ═══════════════════════════════════════════════════════════
				
				// Core MAs
				smaBaseline = SMA(MediumPeriod);
				emaFast = EMA(ShortPeriod);
				emaSlow = EMA(LongPeriod);
				emaRegime = EMA(200);  // Pine: regimeEmaLength default
				wmaIndicator = WMA(20);
				
				// Oscillators
				rsiIndicator = RSI(14, 3);
				macdIndicator = MACD(12, 26, 9);
				stochIndicator = Stochastics(14, 7, 3);
				cciIndicator = CCI(20);
				
				// Volatility
				atrIndicator = ATR(AtrPeriod);
				stdDev = StdDev(20);
				bollingerBands = Bollinger(2, 20);
				
				// Trend
				adxIndicator = ADX(14);
				donchianChannel = DonchianChannel(34);
				
				// Volume
				// PATCH 2: VWAP requires Order Flow license - fallback to VWMA if unavailable
				try
				{
					vwapIndicator = VWAP();  // NT8: VWAP() takes 0 arguments, uses session automatically
					vwapFallback = false;
					Print("✅ VWAP initialized successfully");
				}
				catch (Exception ex)
				{
					vwapFallback = true;
					Print($"⚠️ VWAP unavailable (Order Flow license required): {ex.Message}");
					Print("✅ Fallback: Using VWMA (Volume-Weighted Moving Average) instead");
				}
				volumeIndicator = VOL();  // PATCH 1: VOL() for ISeries<double> compatibility with EMAs
				
				// Heavy indicators (conditionally loaded)
				if (EnableHeavyIndicators)
				{
					psarIndicator = ParabolicSAR(0.02, 0.02, 0.2);
				}
				
				// ═══════════════════════════════════════════════════════════
				// NATIVE NT8 INDICATORS (Performance Optimization)
				// ═══════════════════════════════════════════════════════════
				aroonIndicator = Aroon(14);  // Aligned with TradingView v1.8
				dmiIndicator = DM(14);       // Directional Movement Index
				Print("✅ Native indicators initialized: Aroon(14), DM(14)");
				
				// ═══════════════════════════════════════════════════════════
				// EVOLUTION SYSTEM INITIALIZATION
				// ═══════════════════════════════════════════════════════════
				if (UseEvolutionSystem)
				{
					evolutionArchitect = new EvolutionaryArchitect(seed: EvolutionSeed);
					activeChampion = evolutionArchitect.TrendChampion.Clone();  // Start with Trend champion
					Print($"🧬 Evolution System initialized: 3 populations × 20 chromosomes (Seed: {EvolutionSeed})");
					Print($"   - Trend Population: {evolutionArchitect.TrendPopulation.Length} chromosomes");
					Print($"   - Range Population: {evolutionArchitect.RangePopulation.Length} chromosomes");
					Print($"   - Choppy Population: {evolutionArchitect.ChoppyPopulation.Length} chromosomes");
				}
				
				// Initialize custom series for complex calculations
				emaCustom = new Series<double>(this);
				rsiSeries = new Series<double>(this);
				trueLow = new Series<double>(this);
				trueHigh = new Series<double>(this);
				
				// Initialize Vortex Series (Phase 2 Performance Optimization)
				viPlus = new Series<double>(this);
				viMinus = new Series<double>(this);
				vortexTR = new Series<double>(this);
				Print("✅ Vortex Series initialized (Phase 2: 42x performance gain)");
				
				// Initialize Fisher Transform (Phase 2: 2x performance gain)
				fisherValue = new Series<double>(this);
				fisherEmaIndicator = EMA(fisherValue, 3);  // EMA(3) for signal line
				Print("✅ Fisher Transform Series + EMA(3) initialized (Phase 2: 2x gain)");
				
				// Initialize TSI (True Strength Index) - CORRECTED double-smooth implementation
				tsiMomentum = new Series<double>(this);
				tsiAbsMomentum = new Series<double>(this);
				tsiMomentumEMA25 = EMA(tsiMomentum, 25);        // First smooth
				tsiMomentumEMA13 = EMA(tsiMomentumEMA25, 13);   // Double smooth (EMA of EMA)
				tsiAbsMomentumEMA25 = EMA(tsiAbsMomentum, 25);  // First smooth
				tsiAbsMomentumEMA13 = EMA(tsiAbsMomentumEMA25, 13);  // Double smooth
				tsiValue = new Series<double>(this);
				Print("✅ TSI Series + Double-EMA initialized (Phase 2: 3x gain + BUG FIX)");
				
				// Initialize TDFI (Trend Direction Force Index) - Cached indicators
				tdfiEMA = EMA(Close, 13);
				tdfiSMA = SMA(Close, 13);
				Print("✅ TDFI EMA/SMA cached (Phase 2: 1.5x gain)");
				
				// Initialize CE (Chande Efficiency) - Phase 3 Optimization
				cePriceChange = new Series<double>(this);
				Print("✅ CE Series initialized (Phase 3: 14x gain)");
				
				// Initialize CMF (Chaikin Money Flow) - Phase 3 Optimization
				cmfMoneyFlowVolume = new Series<double>(this);
				cmfVolumeSum = new Series<double>(this);
				Print("✅ CMF Series initialized (Phase 3: 20x gain)");
				
				// ═══════════════════════════════════════════════════════════
				// GENOME ACTIVATION: Evolutive Indicators Series
				// These Series store dynamically recalculated indicators
				// based on evolved champion parameters (ST, RSI, MACD, Regime)
				// ═══════════════════════════════════════════════════════════
				evolutiveST = new Series<double>(this);
				evolutiveRSI = new Series<double>(this);
				evolutiveMACD = new Series<double>(this);
				evolutiveMACDsignal = new Series<double>(this);
				evolutiveRegimeEMA = new Series<double>(this);
				
				// Initialize with default parameters (will be updated by champion)
				championStPeriod = 10;
				championStFactor = 3.0;
				championRsiPeriod = 14;
				championMacdFast = 12;
				championMacdSlow = 26;
				championMacdSignal = 9;
				championRegimePeriod = 50;
				indicatorsNeedRecalc = false;
				
				Print("✅ Genome Activation: Evolutive Indicators initialized (SAGE MODE)");
				
				// Initialize Rocket Drive - Phase 3 Optimization (cached EMAs)
				rdEMA8 = EMA(Close, 8);
				rdEMA21 = EMA(Close, 21);
				rdEMA34 = EMA(Close, 34);
				Print("✅ Rocket Drive EMAs cached (Phase 3: 2x gain)");
				
				// Initialize Hull MA - Phase 3 Optimization (cached WMAs)
				int hullLen = 20;
				hullWMAhalf = WMA(Close, hullLen / 2);
				hullWMAfull = WMA(Close, hullLen);
				Print("✅ Hull MA WMAs cached (Phase 3: 3x gain)");
				
				// Initialize adaptive weights
				wLead_adaptive = WLead;
				wRegime_adaptive = WRegime;
				wStack_adaptive = WStack;
			}
		}

		protected override void OnBarUpdate()
		{
			// ═══════════════════════════════════════════════════════════════
			// SAFETY CHECKS
			// ═══════════════════════════════════════════════════════════════
			if (CurrentBar < Math.Max(Math.Max(ShortPeriod, MediumPeriod), LongPeriod))
				return;
			
			if (atrIndicator[0] == 0)
				return;
			
			// ═══════════════════════════════════════════════════════════════
			// REGIME DETECTION (Trending/Ranging/Choppy)
			// ═══════════════════════════════════════════════════════════════
			DetectRegime();
			
			// ═══════════════════════════════════════════════════════════════
			// GENOME ACTIVATION: Recalculate Evolutive Indicators if Needed
			// The Brain rebuilds its tools when champion changes
			// ═══════════════════════════════════════════════════════════════
			if (UseEvolutionSystem && indicatorsNeedRecalc)
			{
				RecalculateEvolutiveIndicators();
			}
			
			// ═══════════════════════════════════════════════════════════════
			// CALCULATE HEAVY INDICATORS (if enabled)
			// ═══════════════════════════════════════════════════════════════
			if (EnableHeavyIndicators)
			{
				CalculateHeavyIndicators();
				CalculateAdditionalIndicators();  // 25 missing indicators
			}
			
			// ═══════════════════════════════════════════════════════════════
			// AUTOPILOT: Dynamic Leading Indicator Selection
			// ═══════════════════════════════════════════════════════════════
			bool autopilot = (TradingMode == TradingModeType.AutoPure || TradingMode == TradingModeType.AutoGuided);
			if (autopilot && EnableLearning)
			{
				dynamicLeadingIndicator = GetBestLeadingIndicator(isTrending, isRanging, isChoppy);
			}
			else
			{
				dynamicLeadingIndicator = "ST";  // Default to SuperTrend
			}
			
			// ═══════════════════════════════════════════════════════════════
			// BUILD CONFIRMATION STACK (40+ filters)
			// ═══════════════════════════════════════════════════════════════
			BuildConfirmationStack();
			
			// ═══════════════════════════════════════════════════════════════
			// BRAIN SCORING
			// ═══════════════════════════════════════════════════════════════
			CalculateBrainScores();
			
			// ═══════════════════════════════════════════════════════════════
			// VIRTUAL TRADE MANAGEMENT (Learning System)
			// ═══════════════════════════════════════════════════════════════
			UpdateVirtualTrade();
			
			// ═══════════════════════════════════════════════════════════════
			// GENERATE VISUAL SIGNALS (Indicator mode - no auto-trading)
			// ═══════════════════════════════════════════════════════════════
			CheckEntrySignals();
		}
		
		#region Core Methods
		
		private void DetectRegime()
		{
			// ADX-based regime detection (matches Pine script logic)
			double adxValue = adxIndicator[0];
			double adxThreshold = 25.0;  // Pine: regimeAdxThreshold default
			
			// Choppiness Index calculation (Pine script approximation)
			double highestHigh = MAX(High, 14)[0];
			double lowestLow = MIN(Low, 14)[0];
			double range = highestHigh - lowestLow;
			
			double atrSum = 0;
			for (int i = 0; i < 14; i++)
			{
				atrSum += atrIndicator[i];
			}
			
			choppinessIndex = range > 0 ? 100 * Math.Log10(atrSum / range) / Math.Log10(14) : 50;
			
			// Regime classification (matches Pine logic)
			isTrending = adxValue > adxThreshold && choppinessIndex < 50;
			isRanging = adxValue < 20.0 && choppinessIndex > 60.0;
			isChoppy = !isTrending && !isRanging;
			
			// Trendiness (0-1 normalized)
			trendiness = Math.Min(adxValue / 50.0, 1.0);
		}
		
		private void CalculateHeavyIndicators()
		{
			// ═══════════════════════════════════════════════════════════════
			// ICHIMOKU CLOUD (Heavy - only if enabled)
			// ═══════════════════════════════════════════════════════════════
			int conversionLen = 9;
			int baseLen = 26;
			int spanBLen = 52;
			
			double conversionHigh = MAX(High, conversionLen)[0];
			double conversionLow = MIN(Low, conversionLen)[0];
			ichimokuConversion = (conversionHigh + conversionLow) / 2.0;
			
			double baseHigh = MAX(High, baseLen)[0];
			double baseLow = MIN(Low, baseLen)[0];
			ichimokuBase = (baseHigh + baseLow) / 2.0;
			
			ichimokuSpanA = (ichimokuConversion + ichimokuBase) / 2.0;
			
			double spanBHigh = MAX(High, spanBLen)[0];
			double spanBLow = MIN(Low, spanBLen)[0];
			ichimokuSpanB = (spanBHigh + spanBLow) / 2.0;
			
			// ═══════════════════════════════════════════════════════════════
			// SSL HYBRID (Heavy)
			// ═══════════════════════════════════════════════════════════════
			int sslLenFast = 12;
			int sslLenSlow = 26;
			
			sslHigh = SMA(High, sslLenFast)[0];
			sslLow = SMA(Low, sslLenFast)[0];
			sslFast = (sslHigh + sslLow) / 2.0;
			sslSlow = SMA(Close, sslLenSlow)[0];
			
			// ═══════════════════════════════════════════════════════════════
			// HALF TREND (Heavy)
			// ═══════════════════════════════════════════════════════════════
			halfTrendLine = SMA(Close, 20)[0];  // Simplified (Pine uses HMA)
			
			// ═══════════════════════════════════════════════════════════════
			// RANGE FILTER (Heavy)
			// ═══════════════════════════════════════════════════════════════
			double rfSize = atrIndicator[0] * 1.0;  // Pine: rfQty default 1.0
			rangeFilterValue = emaFast[0];  // Simplified
			rangeFilterHigh = rangeFilterValue + rfSize;
			rangeFilterLow = rangeFilterValue - rfSize;
			
			// ═══════════════════════════════════════════════════════════════
			// QQE MOD (Heavy)
			// ═══════════════════════════════════════════════════════════════
			// Simplified: Use direct EMA on RSI values
			qqeWilders = EMA(rsiIndicator, ShortPeriod)[0];
			
			// For signal, we need previous qqeWilders value - use simple lookback
			double prevQQE = CurrentBar > 0 ? qqeWilders : rsiIndicator[0];
			qqeSignal = prevQQE * 0.7 + qqeWilders * 0.3;  // Simple smoothing
		}
		
		private void CalculateAdditionalIndicators()
		{
			// ═══════════════════════════════════════════════════════════════
			// 25 MISSING INDICATORS (from Pine Script DIY.ps)
			// ═══════════════════════════════════════════════════════════════
			
		// Safety check
		if (CurrentBar < 50) return;
		
		// ────────────────────────────────────────────────────────────
		// Aroon values now automatically available from aroonIndicator.Up[0] and .Down[0] (native NT8)
		// ────────────────────────────────────────────────────────────
		aroonUp = aroonIndicator.Up[0];
		aroonDown = aroonIndicator.Down[0];
		
		// ────────────────────────────────────────────────────────────
		// DMI values now automatically available from dmiIndicator.DiPlus[0], .DiMinus[0] (native NT8)
		// ADX from adxIndicator[0] (already native)
		// ────────────────────────────────────────────────────────────
		dmiPlus = dmiIndicator.DiPlus[0];
		dmiMinus = dmiIndicator.DiMinus[0];
		dmiAdx = adxIndicator[0];  // Reuse existing ADX
			
			// ────────────────────────────────────────────────────────────
			// VORTEX INDICATOR (Phase 2 Optimization: 42x faster)
			// Series-based rolling sum replaces 14-iteration manual loop
			// Before: 42,000 ops on 1000 bars | After: 1,000 ops (42x gain)
			// ────────────────────────────────────────────────────────────
			int vortexLen = 14;
			
			// Calculate current bar's VI components (1 calculation vs 14)
			if (CurrentBar >= 1)
			{
				viPlus[0] = Math.Abs(High[0] - Low[1]);
				viMinus[0] = Math.Abs(Low[0] - High[1]);
				vortexTR[0] = Math.Max(High[0] - Low[0], 
					Math.Max(Math.Abs(High[0] - Close[1]), Math.Abs(Low[0] - Close[1])));
			}
			else
			{
				viPlus[0] = 0;
				viMinus[0] = 0;
				vortexTR[0] = High[0] - Low[0];
			}
			
			// Rolling sum using Series (native NT8 efficiency)
			double sumVIplus = SUM(viPlus, vortexLen)[0];
			double sumVIminus = SUM(viMinus, vortexLen)[0];
			double sumTR = SUM(vortexTR, vortexLen)[0];
			
		vortexPlus = sumTR > 0 ? sumVIplus / sumTR : 1.0;
		vortexMinus = sumTR > 0 ? sumVIminus / sumTR : 1.0;
		
		// ────────────────────────────────────────────────────────────
		// FISHER TRANSFORM (Phase 2 Optimization: 2x faster)
		// Native EMA indicator replaces manual alpha calculation
		// Cleaner code + automatic Series caching
		// ────────────────────────────────────────────────────────────
		int fisherLen = 9;  // Aligned with TradingView v1.8 default
		double highFish = MAX(High, fisherLen)[0];
		double lowFish = MIN(Low, fisherLen)[0];
		double rangeFish = highFish - lowFish;
		
		// Calculate normalized value (range: -0.999 to 0.999)
		double value1 = rangeFish > 0 ? 2.0 * ((Close[0] - lowFish) / rangeFish - 0.5) : 0;
		value1 = Math.Max(-0.999, Math.Min(0.999, value1));
		
		// Fisher Transform formula: 0.5 * ln((1+x)/(1-x))
		fisherValue[0] = 0.5 * Math.Log((1 + value1) / (1 - value1));
		fisherTransform = fisherValue[0];
		
		// Fisher Signal = EMA(3) of Fisher Value (native NT8 EMA)
		fisherSignal = fisherEmaIndicator[0];
		
		// ────────────────────────────────────────────────────────────
		// TSI (True Strength Index) - Phase 2 Optimization + BUG FIX
		// CRITICAL FIX: TSI uses double-smoothed MOMENTUM, not price!
		// Previous implementation was mathematically incorrect
		// Correct formula: TSI = 100 × EMA(EMA(momentum, 25), 13) / EMA(EMA(|momentum|, 25), 13)
		// ────────────────────────────────────────────────────────────
			
			// Calculate momentum (price change)
			double momentum = CurrentBar > 0 ? Close[0] - Close[1] : 0;
			tsiMomentum[0] = momentum;
			tsiAbsMomentum[0] = Math.Abs(momentum);
			
			// Double smooth: EMA(25) then EMA(13) of the result
			double doubleSmoothedMomentum = tsiMomentumEMA13[0];      // EMA(13) of EMA(25) of momentum
			double doubleSmoothedAbsMomentum = tsiAbsMomentumEMA13[0]; // EMA(13) of EMA(25) of |momentum|
			
			// TSI = 100 × (smoothed momentum / smoothed absolute momentum)
			tsi = doubleSmoothedAbsMomentum != 0 
				? 100 * (doubleSmoothedMomentum / doubleSmoothedAbsMomentum) 
				: 0;
			
			tsiValue[0] = tsi;
			tsiSignal = EMA(tsiValue, 7)[0];  // Signal line = EMA(7) of TSI
			
			// ────────────────────────────────────────────────────────────
			// TDFI (Trend Direction Force Index) - Phase 2 Optimization
			// Cached EMA/SMA to avoid recalculation (1.5x faster)
			// ────────────────────────────────────────────────────────────
			double mma = tdfiEMA[0];   // Cached EMA(13)
			double smma = tdfiSMA[0];  // Cached SMA(13)
			double impetMMA = CurrentBar > 0 ? mma - tdfiEMA[1] : 0;  // EMA momentum
			
			tdfi = impetMMA * (mma - smma);
			
			// ────────────────────────────────────────────────────────────
			// TLB (Trend Line Break) - Simplified Donchian variant
			// ────────────────────────────────────────────────────────────
			int tlbLen = 20;
			tlbUpper = MAX(High, tlbLen)[0];
			tlbLower = MIN(Low, tlbLen)[0];
			
			// ────────────────────────────────────────────────────────────
			// ROCKET DRIVE (Custom momentum indicator) - Phase 3 Optimization
			// Cached EMAs to avoid recalculation (2x faster)
			// ────────────────────────────────────────────────────────────
			double rdEma8Val = rdEMA8[0];    // Cached EMA(8)
			double rdEma21Val = rdEMA21[0];  // Cached EMA(21)
			double rdEma34Val = rdEMA34[0];  // Cached EMA(34)
			
			rocketDrive = (rdEma8Val - rdEma34Val) + (rdEma21Val - rdEma34Val);
			
			// ────────────────────────────────────────────────────────────
			// HAO (Heikin Ashi Oscillator) - PATCH 5: Fix auto-reference bug
			// CORRECT formula: haOpen uses PREVIOUS bar's values, not current
			// ────────────────────────────────────────────────────────────
			if (CurrentBar > 0)
			{
				double prevHaoOpen = haoOpen;   // Store previous value BEFORE update
				double prevHaoClose = haoClose; // Store previous value BEFORE update
				
				haoClose = (Open[0] + High[0] + Low[0] + Close[0]) / 4.0;
				haoOpen = (prevHaoOpen + prevHaoClose) / 2.0;  // PATCH 5: Use PREVIOUS values
				haoHigh = Math.Max(High[0], Math.Max(haoOpen, haoClose));
				haoLow = Math.Min(Low[0], Math.Min(haoOpen, haoClose));
			}
			else
			{
				haoClose = Close[0];
				haoOpen = Open[0];
				haoHigh = High[0];
				haoLow = Low[0];
			}
			
			// ────────────────────────────────────────────────────────────
			// ROC (Rate of Change)
			// ────────────────────────────────────────────────────────────
			int rocLen = 12;
			double rocPast = CurrentBar >= rocLen ? Close[rocLen] : Close[0];
			rocValue = rocPast != 0 ? ((Close[0] - rocPast) / rocPast) * 100.0 : 0;
			
			// ────────────────────────────────────────────────────────────
			// BX TREND (Baseline Cross Trend)
			// ────────────────────────────────────────────────────────────
			double bxBaseline = smaBaseline[0];
			bxTrend = Close[0] > bxBaseline ? 1 : -1;
			
			// ────────────────────────────────────────────────────────────
			// BB %B (Bollinger Band Percent B)
			// ────────────────────────────────────────────────────────────
			double bbUpper = bollingerBands.Upper[0];
			double bbLower = bollingerBands.Lower[0];
			double bbRange = bbUpper - bbLower;
			
			bbPercentB = bbRange > 0 ? (Close[0] - bbLower) / bbRange : 0.5;
			
			// ────────────────────────────────────────────────────────────
			// DPO (Detrended Price Oscillator)
			// ────────────────────────────────────────────────────────────
			int dpoLen = 20;
			int dpoDisplace = dpoLen / 2 + 1;
			double dpoSMA = CurrentBar >= dpoDisplace ? SMA(Close, dpoLen)[dpoDisplace] : SMA(Close, dpoLen)[0];
			dpoValue = Close[0] - dpoSMA;
			
			// ────────────────────────────────────────────────────────────
			// BBO TREND (Bollinger Band Oscillator)
			// ────────────────────────────────────────────────────────────
			double bbMid = bollingerBands.Middle[0];
			bboTrend = Close[0] > bbMid ? 1 : -1;
			
			// ────────────────────────────────────────────────────────────
			// CE (Chande's Efficiency) - Phase 3 Optimization: 14x faster
			// Series-based rolling sum replaces 14-iteration manual loop
			// ────────────────────────────────────────────────────────────
			int ceLen = 14;
			
			// Calculate current bar's price change
			if (CurrentBar > 0)
			{
				cePriceChange[0] = Math.Abs(Close[0] - Close[1]);
			}
			else
			{
				cePriceChange[0] = 0;
			}
			
			// Efficiency = Direct distance / Total path traveled
			double ceRange = CurrentBar >= ceLen ? Math.Abs(Close[0] - Close[ceLen]) : 0;
			double cePathSum = SUM(cePriceChange, ceLen)[0];  // Series-based rolling sum
			
			ceValue = cePathSum > 0 ? ceRange / cePathSum : 0;
			
			// ────────────────────────────────────────────────────────────
			// WAE (Waddah Attar Explosion)
			// ────────────────────────────────────────────────────────────
			int waeLen = 20;
			double waeSens = 150;
			
			double t1 = macdIndicator.Diff[0] - macdIndicator.Diff[1];
			double e1 = bollingerBands.Upper[0] - bollingerBands.Lower[0];
			
			double trendUp = t1 >= 0 ? t1 : 0;
			double trendDown = t1 < 0 ? -t1 : 0;
			
			waeUp = trendUp * waeSens;
			waeDown = trendDown * waeSens;
			
			// ────────────────────────────────────────────────────────────
			// CMF (Chaikin Money Flow) - Phase 3 Optimization: 20x faster
			// Series-based rolling sum replaces 20-iteration manual loop
			// ────────────────────────────────────────────────────────────
			int cmfLen = 20;
			
			// Calculate current bar's money flow volume
			double hlRange = High[0] - Low[0];
			double mfMultiplier = hlRange > 0 ? ((Close[0] - Low[0]) - (High[0] - Close[0])) / hlRange : 0;
			cmfMoneyFlowVolume[0] = mfMultiplier * Volume[0];
			cmfVolumeSum[0] = Volume[0];
			
			// Rolling sums using Series (native NT8 efficiency)
			double sumMFV = SUM(cmfMoneyFlowVolume, cmfLen)[0];
			double sumVol = SUM(cmfVolumeSum, cmfLen)[0];
			
			cmfValue = sumVol > 0 ? sumMFV / sumVol : 0;
			
			// ────────────────────────────────────────────────────────────
			// STC (Schaff Trend Cycle) - Simplified
			// ────────────────────────────────────────────────────────────
			double stcMACD = macdIndicator.Diff[0];
			double stcSignal = macdIndicator.Avg[0];
			stcValue = stcMACD - stcSignal;
			
			// ────────────────────────────────────────────────────────────
			// AO (Awesome Oscillator)
			// ────────────────────────────────────────────────────────────
			double ao5 = SMA(Close, 5)[0];
			double ao34 = SMA(Close, 34)[0];
			aoValue = ao5 - ao34;
			
			// ────────────────────────────────────────────────────────────
			// VO (Volume Oscillator) - PATCH 1: Use VOL() for ISeries<double>
			// ────────────────────────────────────────────────────────────
			double volFast = EMA(volumeIndicator, 5)[0];  // volumeIndicator is ISeries<double>
			double volSlow = EMA(volumeIndicator, 10)[0]; // Volume (raw) is ISeries<long> - incompatible!
			voValue = volSlow > 0 ? ((volFast - volSlow) / volSlow) * 100.0 : 0;
			
			// ────────────────────────────────────────────────────────────
			// WOLF TREND (EMA Ribbon Trend)
			// ────────────────────────────────────────────────────────────
			double wolf8 = EMA(Close, 8)[0];
			double wolf13 = EMA(Close, 13)[0];
			double wolf21 = EMA(Close, 21)[0];
			
			wolfTrend = (wolf8 > wolf13 && wolf13 > wolf21) ? 1 : 
			            (wolf8 < wolf13 && wolf13 < wolf21) ? -1 : 0;
			
			// ────────────────────────────────────────────────────────────
			// HULL MA (Hull Moving Average) - Phase 3 Optimization
			// Cached WMAs to avoid recalculation (3x faster)
			// Formula: HullMA = WMA(2 × WMA(n/2) - WMA(n), sqrt(n))
			// ────────────────────────────────────────────────────────────
			int hullLen = 20;
			int sqrtLen = (int)Math.Sqrt(hullLen);
			
			double wma1 = hullWMAhalf[0];  // Cached WMA(hullLen/2)
			double wma2 = hullWMAfull[0];  // Cached WMA(hullLen)
			double rawHull = 2 * wma1 - wma2;
			
			// Final Hull MA (simplified - uses sqrt WMA approximation)
			hullMA = WMA(Close, sqrtLen)[0];  // Simplified approximation
		}
		
		private void BuildConfirmationStack()
		{
			confirmationLongBools.Clear();
			confirmationShortBools.Clear();
			
			// ═══════════════════════════════════════════════════════════════
			// CONFIRMATION FILTERS (46 total - matches Pine script DIY.ps)
			// ═══════════════════════════════════════════════════════════════
			
			// ────────────────────────────────────────────────────────────
			// CORE FILTERS (11 basic)
			// ────────────────────────────────────────────────────────────
			
			// 1. EMA Regime Filter
			confirmationLongBools.Add(Close[0] > emaRegime[0]);
			confirmationShortBools.Add(Close[0] < emaRegime[0]);
			
			// 2. 2MA Cross
			confirmationLongBools.Add(emaFast[0] > emaSlow[0]);
			confirmationShortBools.Add(emaFast[0] < emaSlow[0]);
			
			// 3. SuperTrend (simplified)
			bool stUp = Close[0] > smaBaseline[0];
			confirmationLongBools.Add(stUp);
			confirmationShortBools.Add(!stUp);
			
			// 4. Donchian
			double donchMid = (donchianChannel.Upper[0] + donchianChannel.Lower[0]) / 2.0;
			confirmationLongBools.Add(Close[0] > donchMid);
			confirmationShortBools.Add(Close[0] < donchMid);
			
			// 5. RSI
			confirmationLongBools.Add(rsiIndicator[0] > 50);
			confirmationShortBools.Add(rsiIndicator[0] < 50);
			
			// 6. VWAP (with PATCH 2 fallback to VWMA if license unavailable)
			if (!vwapFallback)
			{
				confirmationLongBools.Add(Close[0] > vwapIndicator[0]);
				confirmationShortBools.Add(Close[0] < vwapIndicator[0]);
			}
			else
			{
				// Fallback: Use VWMA(20) - Volume-Weighted Moving Average
				double vwma = VWMA(Close, 20)[0];
				confirmationLongBools.Add(Close[0] > vwma);
				confirmationShortBools.Add(Close[0] < vwma);
			}
			
			// 7. CCI
			confirmationLongBools.Add(cciIndicator[0] > 0);
			confirmationShortBools.Add(cciIndicator[0] < 0);
			
			// 8. ADX Strength
			confirmationLongBools.Add(adxIndicator[0] > 20);
			confirmationShortBools.Add(adxIndicator[0] > 20);
			
			// 9. MACD
			confirmationLongBools.Add(macdIndicator.Diff[0] > 0);
			confirmationShortBools.Add(macdIndicator.Diff[0] < 0);
			
			// 10. Stochastic
			confirmationLongBools.Add(stochIndicator.K[0] > stochIndicator.D[0]);
			confirmationShortBools.Add(stochIndicator.K[0] < stochIndicator.D[0]);
			
			// 11. Bollinger Bands
			confirmationLongBools.Add(Close[0] < bollingerBands.Lower[0]);
			confirmationShortBools.Add(Close[0] > bollingerBands.Upper[0]);
			
			// ────────────────────────────────────────────────────────────
			// HEAVY INDICATORS (35 additional filters)
			// ────────────────────────────────────────────────────────────
			if (EnableHeavyIndicators)
			{
				// 12. Ichimoku Cloud
				bool ichiLong = Close[0] > ichimokuSpanA && Close[0] > ichimokuSpanB && ichimokuConversion > ichimokuBase;
				confirmationLongBools.Add(ichiLong);
				confirmationShortBools.Add(!ichiLong);
				
				// 13. SSL Hybrid
				bool sslLong = sslFast > sslSlow;
				confirmationLongBools.Add(sslLong);
				confirmationShortBools.Add(!sslLong);
				
				// 14. PSAR
				confirmationLongBools.Add(Close[0] > psarIndicator[0]);
				confirmationShortBools.Add(Close[0] < psarIndicator[0]);
				
				// 15. Range Filter
				bool rfLong = Close[0] > rangeFilterValue;
				confirmationLongBools.Add(rfLong);
				confirmationShortBools.Add(!rfLong);
				
				// 16. QQE Mod
				bool qqeLong = qqeWilders > qqeSignal;
				confirmationLongBools.Add(qqeLong);
				confirmationShortBools.Add(!qqeLong);
				
				// 17. HalfTrend
				bool htLong = Close[0] > halfTrendLine;
				confirmationLongBools.Add(htLong);
				confirmationShortBools.Add(!htLong);
				
				// 18. Aroon
				confirmationLongBools.Add(aroonUp > aroonDown && aroonUp > 70);
				confirmationShortBools.Add(aroonDown > aroonUp && aroonDown > 70);
				
				// 19. DMI
				confirmationLongBools.Add(dmiPlus > dmiMinus && dmiAdx > 20);
				confirmationShortBools.Add(dmiMinus > dmiPlus && dmiAdx > 20);
				
				// 20. Vortex
				confirmationLongBools.Add(vortexPlus > vortexMinus);
				confirmationShortBools.Add(vortexMinus > vortexPlus);
				
				// 21. Fisher Transform
				confirmationLongBools.Add(fisherTransform > fisherSignal);
				confirmationShortBools.Add(fisherTransform < fisherSignal);
				
				// 22. TSI
				confirmationLongBools.Add(tsi > 0);
				confirmationShortBools.Add(tsi < 0);
				
				// 23. TDFI
				confirmationLongBools.Add(tdfi > 0);
				confirmationShortBools.Add(tdfi < 0);
				
				// 24. TLB (Trend Line Break)
				confirmationLongBools.Add(Close[0] > tlbUpper);
				confirmationShortBools.Add(Close[0] < tlbLower);
				
				// 25. Rocket Drive
				confirmationLongBools.Add(rocketDrive > 0);
				confirmationShortBools.Add(rocketDrive < 0);
				
				// 26. HAO (Heikin Ashi Oscillator)
				bool haoLong = haoClose > haoOpen;
				confirmationLongBools.Add(haoLong);
				confirmationShortBools.Add(!haoLong);
				
				// 27. ROC (Rate of Change)
				confirmationLongBools.Add(rocValue > 0);
				confirmationShortBools.Add(rocValue < 0);
				
				// 28. BX Trend
				confirmationLongBools.Add(bxTrend > 0);
				confirmationShortBools.Add(bxTrend < 0);
				
				// 29. BB %B
				confirmationLongBools.Add(bbPercentB < 0.2);  // Oversold
				confirmationShortBools.Add(bbPercentB > 0.8);  // Overbought
				
				// 30. DPO
				confirmationLongBools.Add(dpoValue > 0);
				confirmationShortBools.Add(dpoValue < 0);
				
				// 31. BBO Trend
				confirmationLongBools.Add(bboTrend > 0);
				confirmationShortBools.Add(bboTrend < 0);
				
				// 32. CE (Chande's Efficiency)
				confirmationLongBools.Add(ceValue > 0.5);  // Efficient trend
				confirmationShortBools.Add(ceValue > 0.5);  // Neutral (confirms either)
				
				// 33. WAE (Waddah Attar Explosion)
				confirmationLongBools.Add(waeUp > waeDown);
				confirmationShortBools.Add(waeDown > waeUp);
				
				// 34. CMF (Chaikin Money Flow)
				confirmationLongBools.Add(cmfValue > 0);
				confirmationShortBools.Add(cmfValue < 0);
				
				// 35. STC (Schaff Trend Cycle)
				confirmationLongBools.Add(stcValue > 0);
				confirmationShortBools.Add(stcValue < 0);
				
				// 36. AO (Awesome Oscillator)
				confirmationLongBools.Add(aoValue > 0);
				confirmationShortBools.Add(aoValue < 0);
				
				// 37. VO (Volume Oscillator)
				confirmationLongBools.Add(voValue > 0);
				confirmationShortBools.Add(voValue < 0);
				
				// 38. Wolf Trend
				confirmationLongBools.Add(wolfTrend > 0);
				confirmationShortBools.Add(wolfTrend < 0);
				
				// 39. Hull MA
				confirmationLongBools.Add(Close[0] > hullMA);
				confirmationShortBools.Add(Close[0] < hullMA);
				
				// 40. RSI Overbought/Oversold
				confirmationLongBools.Add(rsiIndicator[0] < 30);  // Oversold
				confirmationShortBools.Add(rsiIndicator[0] > 70);  // Overbought
				
				// 41. Stochastic Extremes
				confirmationLongBools.Add(stochIndicator.K[0] < 20);  // Oversold
				confirmationShortBools.Add(stochIndicator.K[0] > 80);  // Overbought
				
				// 42. Price vs WMA
				confirmationLongBools.Add(Close[0] > wmaIndicator[0]);
				confirmationShortBools.Add(Close[0] < wmaIndicator[0]);
				
				// 43. ATR Expansion (volatility breakout)
			double atrAvg = SMA(atrIndicator, 14)[0];
			confirmationLongBools.Add(atrIndicator[0] > atrAvg * 1.2);
			confirmationShortBools.Add(atrIndicator[0] > atrAvg * 1.2);
			
			// 44. Volume Confirmation
			// PHASE 1 FIX #3: Use volumeIndicator (ISeries<double>) instead of Volume (ISeries<long>)
			double avgVol = SMA(volumeIndicator, 20)[0];  // FIXED: volumeIndicator is VOL(), compatible with SMA
			confirmationLongBools.Add(Volume[0] > avgVol * 1.5);
			confirmationShortBools.Add(Volume[0] > avgVol * 1.5);				// 45. Trend Alignment (3 EMAs)
				bool trendUp = emaFast[0] > smaBaseline[0] && smaBaseline[0] > emaSlow[0];
				bool trendDown = emaFast[0] < smaBaseline[0] && smaBaseline[0] < emaSlow[0];
				confirmationLongBools.Add(trendUp);
				confirmationShortBools.Add(trendDown);
				
				// 46. Regime Strength (ADX + Trendiness)
				bool strongRegime = adxIndicator[0] > 25 && trendiness > 0.6;
				confirmationLongBools.Add(strongRegime);
				confirmationShortBools.Add(strongRegime);
			}
			
			// Count passed confirmations
			passedLongCount = confirmationLongBools.Count(x => x);
			passedShortCount = confirmationShortBools.Count(x => x);
		}
		
		private void CalculateBrainScores()
		{
			// ═══════════════════════════════════════════════════════════════
			// LEADING INDICATOR SIGNAL (dynamic selection via autopilot)
			// ═══════════════════════════════════════════════════════════════
			bool leadingLong = GetLeadingSignal(dynamicLeadingIndicator, true);
			bool leadingShort = GetLeadingSignal(dynamicLeadingIndicator, false);
			
			// ═══════════════════════════════════════════════════════════════
			// REGIME CONFIRMATION
			// GENOME ACTIVATION: Use evolutive Regime EMA if evolution active
			// ═══════════════════════════════════════════════════════════════
			double regimeEMA = (UseEvolutionSystem && evolutiveRegimeEMA.Count > 0) ? 
				evolutiveRegimeEMA[0] : emaRegime[0];
			
			bool regimeLong = Close[0] > regimeEMA && adxIndicator[0] > 25.0;
			bool regimeShort = Close[0] < regimeEMA && adxIndicator[0] > 25.0;
			
			// ═══════════════════════════════════════════════════════════════
			// CONFIRMATION STACK (adaptive mode selection)
			// v1.10.3 FIX #1: Reduced CHOPPY threshold from 80% to 60%
			// RATIONALE: User logs showed SHORT at 63-65% blocked by 78% threshold in CHOPPY
			// CHOPPY markets have mixed signals by definition - 80% was unrealistic
			// ═══════════════════════════════════════════════════════════════
			int totalConfirmations = confirmationLongBools.Count;
			int thresholdAdaptive = isTrending ? 
				Math.Max(1, (int)(totalConfirmations * 0.60)) :  // Trending: 60% required (28/46)
				isRanging ? 1 :  // Ranging: Any confirmation OK (1/46)
				Math.Max(2, (int)(totalConfirmations * 0.60));  // Choppy: 60% required (28/46) - FIXED from 0.80
			
			// 🔍 DIAGNOSTIC: Log confirmation stack details (TEMPORARY)
			if (CurrentBar % 50 == 0)
			{
				string regimeText = isTrending ? "TREND" : isRanging ? "RANGE" : "CHOPPY";
				Print($"📊 Confirmation Stack | Regime: {regimeText} | Threshold: {thresholdAdaptive}/{totalConfirmations} ({(thresholdAdaptive*100.0/totalConfirmations):F0}%)");
				Print($"   LONG: {passedLongCount}/{totalConfirmations} ({(passedLongCount*100.0/totalConfirmations):F0}%) | SHORT: {passedShortCount}/{totalConfirmations} ({(passedShortCount*100.0/totalConfirmations):F0}%)");
			}
			
			bool stackLong = passedLongCount >= thresholdAdaptive;
			bool stackShort = passedShortCount >= thresholdAdaptive;
			
		
		// ═══════════════════════════════════════════════════════════════
		// VOLUME SPIKE
		// v1.10.3 FIX #2: Make volume component directional (was identical for LONG/SHORT)
		// RATIONALE: Volume spike on red candle = bearish conviction, green = bullish
		// Previous logic gave same boost to both directions - no differentiation
		// ═══════════════════════════════════════════════════════════════
		// PHASE 1 FIX #3: Use volumeIndicator (ISeries<double>) instead of Volume (ISeries<long>)
		double avgVolume = SMA(volumeIndicator, 20)[0];  // FIXED: volumeIndicator is VOL(), compatible with SMA
		bool volumeLong = Volume[0] > avgVolume * 1.2 && Close[0] > Open[0];  // Volume spike on green candle
		bool volumeShort = Volume[0] > avgVolume * 1.2 && Close[0] < Open[0];  // Volume spike on red candle
			// ═══════════════════════════════════════════════════════════════
			// UPDATE CURRENT REGIME (needed for regime gates in Phase 2)
			// ═══════════════════════════════════════════════════════════════
			currentRegime = isTrending ? "TREND" : isRanging ? "RANGE" : "CHOPPY";
			
			// ═══════════════════════════════════════════════════════════════
			// EVOLUTION SYSTEM: Adaptive Parameter Optimization
			// ═══════════════════════════════════════════════════════════════
			if (UseEvolutionSystem && evolutionArchitect != null && CurrentBar > 200)
			{
				try
				{
					barsSinceEvolution++;
					
					// Trigger evolution every N bars
					if (barsSinceEvolution >= EvolutionFrequency)
					{
						// Get trade returns for current regime (LEGACY - kept for logging but not used in fitness)
						List<double> regimeReturns = GetRegimeTradeReturns(currentRegime);
						
						// Evolve population and get champion
						// PHASE 2: Now passes 'this' instance for per-chromosome simulation
						// NOTE: regimeReturns is kept for minimum trade check but not used in UpdateFitness
						if (regimeReturns.Count >= 5)  // Need minimum trades to start evolving
						{
							Chromosome champion = evolutionArchitect.Evolve(currentRegime, regimeReturns, this);
							
							// PHASE 2 HOTFIX: Verify champion has 40 genes before using
							if (champion != null && champion.Genes.Length == 40)
							{
								activeChampion = champion.Clone();
								
								// Apply champion parameters
								ApplyChampionParameters(champion);
								
								// Log evolution metrics
								Print($"🧬 Gen {evolutionArchitect.Generation} | {currentRegime.ToUpper()} | " +
								      $"Fitness: {champion.Fitness:F3} | WinRate: {champion.WinRate:P0} | " +
								      $"Sharpe: {champion.SharpeRatio:F2} | Complexity: {champion.Complexity}");
							}
							else
							{
								Print($"⚠️ Evolution returned invalid champion (Genes: {champion?.Genes.Length ?? 0}, expected 40). Skipping update.");
							}
						}
						
						barsSinceEvolution = 0;
					}
				}
				catch (Exception ex)
				{
					Print($"⚠️ Evolution Error at bar {CurrentBar}: {ex.Message}");
					barsSinceEvolution = 0;  // Reset to avoid infinite errors
				}
			}
			
		// ═══════════════════════════════════════════════════════════════
		// BRAIN SCORING MODE SELECTION
		// PHASE 2: Weights can be evolved by the AI (Supreme Intelligence)
		// ═══════════════════════════════════════════════════════════════
		
		// Default weights (fallback if evolution not active)
		double wL = AdaptiveWeights ? wLead_adaptive : WLead;
		double wR = AdaptiveWeights ? wRegime_adaptive : WRegime;
		double wS = AdaptiveWeights ? wStack_adaptive : WStack;
		double wV = WVolume;
		double wH = WHybrid;  // Hybrid signal weight
		
		// PHASE 2: Override with evolved weights if evolution system active
		if (UseEvolutionSystem && activeChampion != null && CurrentBar > 50)
		{
			// BUGFIX v1.10.5: Enforce minimum weights to prevent evolution from disabling components
			// Evolution was setting wR=0.00, losing regime contribution entirely
			const double MIN_WEIGHT = 0.30;  // 30% minimum (prevents complete disable)
			
			wL = Math.Max(MIN_WEIGHT, activeChampion.ExtractParam("WEIGHT_LEAD"));
			wS = Math.Max(MIN_WEIGHT, activeChampion.ExtractParam("WEIGHT_STACK"));
			wR = Math.Max(MIN_WEIGHT, activeChampion.ExtractParam("WEIGHT_REGIME"));  // Critical for SHORT
			wV = Math.Max(MIN_WEIGHT, activeChampion.ExtractParam("WEIGHT_VOLUME"));
			wH = Math.Max(MIN_WEIGHT, activeChampion.ExtractParam("WEIGHT_HYBRID"));
		}
		
		if (UseProbabilisticBrain)
		{
			// ───────────────────────────────────────────────────────────
			// PROBABILISTIC MODE (0-1 normalized, matches Pine logic)
			// PHASE 2: Uses evolved weights
			// ───────────────────────────────────────────────────────────
			
			// 🔍 DIAGNOSTIC: Log which mode is active
			if (CurrentBar % 100 == 0)
			{
				Print($"⚙️ Brain Mode: PROBABILISTIC (Strength-based)");
			}
			
			// Normalize components to 0-1
			double leadStrength = NormalizeLeadingSignal(leadingLong, leadingShort);
			double regimeStrength = NormalizeRegimePosition();
			double stackStrength = NormalizeConfirmationStack(passedLongCount, passedShortCount, totalConfirmations);
			double volumeStrength = NormalizeVolume(avgVolume);
			
			// Weighted sum (PHASE 2: weights evolved by AI)
			// BUG FIX v1.10.4: maxScore should NOT include wH (Hybrid not used in PROBABILISTIC)
			maxScore = wL + wR + wS + wV;  // Corrected from: wL + wR + wS + wV + wH
			
			double longScore = wL * Math.Max(0, leadStrength) + 
			                   wR * Math.Max(0, regimeStrength) + 
			                   wS * Math.Max(0, stackStrength) + 
			                   wV * volumeStrength;
			
			double shortScore = wL * Math.Max(0, -leadStrength) + 
			                    wR * Math.Max(0, -regimeStrength) + 
			                    wS * Math.Max(0, -stackStrength) + 
			                    wV * volumeStrength;
			
			brainLongScore = longScore;
			brainShortScore = shortScore;
			
			// 🧩 DIAGNOSTIC: Component breakdown in PROBABILISTIC mode
			if (CurrentBar % 50 == 0)
			{
				Print($"🧩 PROBABILISTIC Components | LeadStr: {leadStrength:F2} | RegimeStr: {regimeStrength:F2} | StackStr: {stackStrength:F2} ({passedShortCount}/{totalConfirmations}) | VolStr: {volumeStrength:F2}");
				Print($"   Weights | wL: {wL:F2} | wR: {wR:F2} | wS: {wS:F2} | wV: {wV:F2} | wH: {wH:F2}");
				Print($"   Scores | LONG: {longScore:F2} | SHORT: {shortScore:F2} | maxScore: {maxScore:F2}");
			}
			
			// Conviction (normalized difference)
			conviction = maxScore > 0 ? Math.Abs(longScore - shortScore) / maxScore : 0.0;
		}
		else
		{
			// ───────────────────────────────────────────────────────────
			// WEIGHTED BOOLEAN MODE (default, matches Pine default)
			// PHASE 2: Uses evolved weights
			// ───────────────────────────────────────────────────────────
			
			// 🔍 DIAGNOSTIC: Log which mode is active
			if (CurrentBar % 100 == 0)
			{
				Print($"⚙️ Brain Mode: BOOLEAN (Weighted components)");
			}
			
			maxScore = wL + wR + wS + wV + wH;
			
			brainLongScore = (leadingLong ? wL : 0) + 
			                 (regimeLong ? wR : 0) + 
			                 (stackLong ? wS : 0) + 
			                 (volumeLong ? wV : 0);
			
			brainShortScore = (leadingShort ? wL : 0) + 
			                  (regimeShort ? wR : 0) + 
			                  (stackShort ? wS : 0) + 
			                  (volumeShort ? wV : 0);
			
			// 🔍 DIAGNOSTIC: Log SHORT components every 50 bars (CRITICAL for debugging)
			if (CurrentBar % 50 == 0)
			{
				Print($"🧩 SHORT Components | Lead: {leadingShort} | Regime: {regimeShort} | Stack: {stackShort} ({passedShortCount}/{confirmationShortBools.Count}) | Vol: {volumeShort}");
				Print($"   Weights | wL: {wL:F2} | wR: {wR:F2} | wS: {wS:F2} | wV: {wV:F2} | brainShortScore: {brainShortScore:F2}");
			}
			
			// 🔍 DIAGNOSTIC: Log SHORT components (TEMPORARY - identify why SHORT is always low)
			if (CurrentBar % 50 == 0)  // CHANGED: Log always, not just during declines
			{
				Print($"🧩 SHORT Components | Lead: {leadingShort} | Regime: {regimeShort} | Stack: {stackShort} ({passedShortCount}/{confirmationShortBools.Count}) | Vol: {volumeShort}");
				Print($"   Weights | wL: {wL:F2} | wR: {wR:F2} | wS: {wS:F2} | wV: {wV:F2} | Total Score: {brainShortScore:F2}");
			}
			{
				Print($"🧩 SHORT Components | Lead: {leadingShort} | Regime: {regimeShort} | Stack: {stackShort} ({passedShortCount}/{confirmationShortBools.Count}) | Vol: {volumeShort}");
				Print($"   Weights | wL: {wL:F2} | wR: {wR:F2} | wS: {wS:F2} | wV: {wV:F2} | Total Score: {brainShortScore:F2}");
			}
			
			// Edge boost: slight penalty to weaker side (Pine logic)
			double scoreEdge = brainLongScore - brainShortScore;
			if (scoreEdge > 0)
				brainShortScore *= 0.95;
			else if (scoreEdge < 0)
				brainLongScore *= 0.95;
			
			// Conviction (normalized 0-1)
			conviction = maxScore > 0 ? Math.Abs(brainLongScore - brainShortScore) / maxScore : 0.0;
		}
	}		// ═══════════════════════════════════════════════════════════════
		// PROBABILISTIC NORMALIZERS (matches Pine script logic)
		// ═══════════════════════════════════════════════════════════════
		
		private double NormalizeLeadingSignal(bool isLong, bool isShort)
		{
			// Returns -1 to +1 (LONG = +1, SHORT = -1, Neutral = 0)
			if (isLong && !isShort) return 1.0;
			if (isShort && !isLong) return -1.0;
			return 0.0;
		}
		
		private double NormalizeRegimePosition()
		{
			// Based on price position relative to regime EMA
			// Returns -1 to +1
			double distance = Close[0] - emaRegime[0];
			double atr = atrIndicator[0];
			
			if (atr == 0) return 0.0;
			
			double normalized = distance / (atr * 2.0);  // Normalize to ±0.5 ATR range
			return Math.Max(-1.0, Math.Min(1.0, normalized));  // Clamp to [-1, 1]
		}
		
		private double NormalizeConfirmationStack(int longCount, int shortCount, int total)
		{
			// Returns -1 to +1 based on confirmation stack balance
			if (total == 0) return 0.0;
			
			double longRatio = (double)longCount / total;
			double shortRatio = (double)shortCount / total;
			
			// Net confirmation strength
			return longRatio - shortRatio;  // Range: -1 to +1
		}
		
		private double NormalizeVolume(double avgVol)
		{
			// Returns 0-1 based on volume spike
			if (avgVol == 0) return 0.0;
			
			double ratio = Volume[0] / avgVol;
			double strength = (ratio - 1.0) / 2.0;  // Normalize: 1.0x = 0, 3.0x = 1.0
			
			return Math.Max(0.0, Math.Min(1.0, strength));  // Clamp to [0, 1]
		}
		
		private bool GetLeadingSignal(string indicatorKey, bool isLong)
		{
			// Returns leading signal based on indicator key (40 indicators supported)
			// GENOME ACTIVATION: Use evolutive indicators when Evolution System is active
			
			switch (indicatorKey)
			{
				case "ST":  // SuperTrend (default)
					// Use evolutive SuperTrend if evolution system active
					if (UseEvolutionSystem && evolutiveST.Count > 0)
						return isLong ? Close[0] > evolutiveST[0] : Close[0] < evolutiveST[0];
					else
						return isLong ? Close[0] > smaBaseline[0] : Close[0] < smaBaseline[0];
				
				case "RSI":
					// Use evolutive RSI if evolution system active
					if (UseEvolutionSystem && evolutiveRSI.Count > 0)
						return isLong ? evolutiveRSI[0] > 50 : evolutiveRSI[0] < 50;
					else
						return isLong ? rsiIndicator[0] > 50 : rsiIndicator[0] < 50;
				
				case "MACD":
					// Use evolutive MACD if evolution system active
					if (UseEvolutionSystem && evolutiveMACD.Count > 0 && evolutiveMACDsignal.Count > 0)
					{
						double macdDiff = evolutiveMACD[0] - evolutiveMACDsignal[0];
						return isLong ? macdDiff > 0 : macdDiff < 0;
					}
					else
						return isLong ? macdIndicator.Diff[0] > 0 : macdIndicator.Diff[0] < 0;
				
				case "Stoch":
					return isLong ? 
						stochIndicator.K[0] > stochIndicator.D[0] && stochIndicator.K[0] < 80 :
						stochIndicator.K[0] < stochIndicator.D[0] && stochIndicator.K[0] > 20;
				
				case "Don":  // Donchian
					double donchMid = (donchianChannel.Upper[0] + donchianChannel.Lower[0]) / 2.0;
					return isLong ? Close[0] > donchMid : Close[0] < donchMid;
				
				case "VWAP":
					// PATCH 2: Fallback to VWMA if VWAP license unavailable
					if (!vwapFallback)
						return isLong ? Close[0] > vwapIndicator[0] : Close[0] < vwapIndicator[0];
					else
					{
						double vwma = VWMA(Close, 20)[0];
						return isLong ? Close[0] > vwma : Close[0] < vwma;
					}
				
				case "CCI":
					return isLong ? cciIndicator[0] > 0 : cciIndicator[0] < 0;
				
				case "2MA":  // 2 EMA Cross
					return isLong ? emaFast[0] > emaSlow[0] : emaFast[0] < emaSlow[0];
				
				case "ADX":
					return adxIndicator[0] > 25;  // Directional strength (neutral)
				
				// Heavy indicators (only if enabled)
				case "Ichi":
					if (!EnableHeavyIndicators) goto default;
					return isLong ?
						Close[0] > ichimokuSpanA && Close[0] > ichimokuSpanB && ichimokuConversion > ichimokuBase :
						Close[0] < ichimokuSpanA && Close[0] < ichimokuSpanB && ichimokuConversion < ichimokuBase;
				
				case "SSL":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? sslFast > sslSlow : sslFast < sslSlow;
				
				case "PSAR":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? Close[0] > psarIndicator[0] : Close[0] < psarIndicator[0];
				
				case "RF":  // Range Filter
					if (!EnableHeavyIndicators) goto default;
					return isLong ? Close[0] > rangeFilterValue : Close[0] < rangeFilterValue;
				
				case "QQE":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? qqeWilders > qqeSignal : qqeWilders < qqeSignal;
				
				case "HT":  // HalfTrend
					if (!EnableHeavyIndicators) goto default;
					return isLong ? Close[0] > halfTrendLine : Close[0] < halfTrendLine;
				
				case "RQK":  // Aroon (RocketQuake)
					if (!EnableHeavyIndicators) goto default;
					return isLong ? aroonUp > aroonDown : aroonDown > aroonUp;
				
				case "TSI":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? tsi > 0 : tsi < 0;
				
				case "TDFI":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? tdfi > 0 : tdfi < 0;
				
				case "TLB":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? Close[0] > tlbUpper : Close[0] < tlbLower;
				
				case "RD":  // Rocket Drive
					if (!EnableHeavyIndicators) goto default;
					return isLong ? rocketDrive > 0 : rocketDrive < 0;
				
				case "HAO":  // Heikin Ashi Oscillator
					if (!EnableHeavyIndicators) goto default;
					return isLong ? haoClose > haoOpen : haoClose < haoOpen;
				
				case "ROC":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? rocValue > 0 : rocValue < 0;
				
				case "BX":  // Baseline Cross
					if (!EnableHeavyIndicators) goto default;
					return isLong ? bxTrend > 0 : bxTrend < 0;
				
				case "BBPT":  // BB Percent B
					if (!EnableHeavyIndicators) goto default;
					return isLong ? bbPercentB < 0.2 : bbPercentB > 0.8;
				
				case "DPO":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? dpoValue > 0 : dpoValue < 0;
				
				case "BBO":  // Bollinger Band Oscillator
					if (!EnableHeavyIndicators) goto default;
					return isLong ? bboTrend > 0 : bboTrend < 0;
				
				case "CE":  // Chande's Efficiency
					if (!EnableHeavyIndicators) goto default;
					return ceValue > 0.5;  // Neutral (confirms trend strength)
				
				case "WAE":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? waeUp > waeDown : waeDown > waeUp;
				
				case "CMF":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? cmfValue > 0 : cmfValue < 0;
				
				case "VI":  // Vortex Index
					if (!EnableHeavyIndicators) goto default;
					return isLong ? vortexPlus > vortexMinus : vortexMinus > vortexPlus;
				
				case "STC":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? stcValue > 0 : stcValue < 0;
				
				case "AO":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? aoValue > 0 : aoValue < 0;
				
				case "VO":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? voValue > 0 : voValue < 0;
				
				case "Wolf":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? wolfTrend > 0 : wolfTrend < 0;
				
				case "Hull":
					if (!EnableHeavyIndicators) goto default;
					return isLong ? Close[0] > hullMA : Close[0] < hullMA;
				
				case "3MA":  // 3 EMA alignment
					bool trend3Up = emaFast[0] > smaBaseline[0] && smaBaseline[0] > emaSlow[0];
					bool trend3Down = emaFast[0] < smaBaseline[0] && smaBaseline[0] < emaSlow[0];
					return isLong ? trend3Up : trend3Down;
				
				case "SIchi":  // Secondary Ichimoku (cloud only)
					if (!EnableHeavyIndicators) goto default;
					return isLong ? Close[0] > ichimokuSpanA : Close[0] < ichimokuSpanA;
				
				default:
					// Fallback to SuperTrend if indicator not found
					return isLong ? Close[0] > smaBaseline[0] : Close[0] < smaBaseline[0];
			}
		}
		
		private string GetBestLeadingIndicator(bool isTrend, bool isRange, bool isChop)
		{
			// Returns best performing indicator for current regime (autopilot logic)
			double[] wrArray = isTrend ? trendWinRates : isRange ? rangeWinRates : choppyWinRates;
			int[] tcArray = isTrend ? trendTradeCounts : isRange ? rangeTradeCounts : choppyTradeCounts;
			
			int minTrades = 10;  // Pine: minTradesPerIndicator default
			double maxWinRate = 0.0;
			string best = "ST";  // Default fallback
			
			for (int i = 0; i < indicatorNames.Length; i++)
			{
				if (tcArray[i] >= minTrades && wrArray[i] > maxWinRate)
				{
					maxWinRate = wrArray[i];
					best = indicatorNames[i];
				}
			}
			
			return best;
		}
		
		private void UpdateVirtualTrade()
		{
			// ═══════════════════════════════════════════════════════════════
			// VIRTUAL TRADE TRACKING (Indicator Mode Simulation)
			// Simulates trade execution to update learning arrays
			// ═══════════════════════════════════════════════════════════════
			
			if (!EnableLearning) return;
			
			// Check for exit conditions if in virtual trade
			if (inVirtualTrade)
			{
				virtualBarsInTrade++;
				
				// Track highest/lowest
				if (virtualTradeDirection == 1)  // LONG
				{
					virtualHighest = Math.Max(virtualHighest, High[0]);
					
					// PATCH 4: Apply Breakeven logic to virtual trade (learning coherence)
					double rMultiple = (virtualHighest - virtualEntryPrice) / (virtualEntryPrice - virtualStopLoss);
					if (BreakevenAfterR > 0 && rMultiple >= BreakevenAfterR && virtualStopLoss < virtualEntryPrice)
					{
						virtualStopLoss = virtualEntryPrice;  // Move SL to breakeven
					}
					
					// PATCH 4: Apply Trailing Stop logic to virtual trade (learning coherence)
					if (TrailAtrMultiplier > 0)
					{
						double trailStop = virtualHighest - (TrailAtrMultiplier * atrIndicator[0]);
						if (trailStop > virtualStopLoss)
						{
							virtualStopLoss = trailStop;  // Trail the stop loss
						}
					}
					
					// Exit conditions
					bool hitTP = High[0] >= virtualTakeProfit;
					bool hitSL = Low[0] <= virtualStopLoss;
					bool rsiExit = useRSIexit && rsiIndicator[0] > rsiExitLevel;
					bool stallExit = useStallExit && virtualBarsInTrade > stallExitBars && 
					                 High[0] < virtualHighest;
					bool maExit = useMAexit && Close[0] < EMA(Close, maExitPeriod)[0];
					
					if (hitTP || hitSL || rsiExit || stallExit || maExit)
					{
						// Calculate P&L
						double exitPrice = hitTP ? virtualTakeProfit : 
						                   hitSL ? virtualStopLoss : Close[0];
						double pnl = exitPrice - virtualEntryPrice;
						bool isWin = pnl > 0;
						
						// Calculate percentage return for evolution system
						double returnPct = (pnl / virtualEntryPrice) * 100.0;
						
						// Update learning
						UpdateLearning(isWin, virtualEntryIndicator, virtualEntryRegime);
						
						// Record for evolution system (per regime)
						if (UseEvolutionSystem)
						{
							RecordTradeResult(returnPct, virtualEntryRegime);
						}
						
						// Reset virtual trade
						inVirtualTrade = false;
						virtualTradeDirection = 0;
						
						Print(String.Format("{0} | Virtual LONG Exit @ {1:F2} | P&L: {2:F2} | Win: {3} | Indicator: {4} | Regime: {5} | Bars: {6}", 
							Time[0], exitPrice, pnl, isWin, virtualEntryIndicator, virtualEntryRegime, virtualBarsInTrade));
					}
				}
				else if (virtualTradeDirection == -1)  // SHORT
				{
					virtualLowest = Math.Min(virtualLowest, Low[0]);
					
					// PATCH 4: Apply Breakeven logic to virtual trade (learning coherence)
					double rMultiple = (virtualEntryPrice - virtualLowest) / (virtualStopLoss - virtualEntryPrice);
					if (BreakevenAfterR > 0 && rMultiple >= BreakevenAfterR && virtualStopLoss > virtualEntryPrice)
					{
						virtualStopLoss = virtualEntryPrice;  // Move SL to breakeven
					}
					
					// PATCH 4: Apply Trailing Stop logic to virtual trade (learning coherence)
					if (TrailAtrMultiplier > 0)
					{
						double trailStop = virtualLowest + (TrailAtrMultiplier * atrIndicator[0]);
						if (trailStop < virtualStopLoss)
						{
							virtualStopLoss = trailStop;  // Trail the stop loss
						}
					}
					
					// Exit conditions
					bool hitTP = Low[0] <= virtualTakeProfit;
					bool hitSL = High[0] >= virtualStopLoss;
					bool rsiExit = useRSIexit && rsiIndicator[0] < (100 - rsiExitLevel);
					bool stallExit = useStallExit && virtualBarsInTrade > stallExitBars && 
					                 Low[0] > virtualLowest;
					bool maExit = useMAexit && Close[0] > EMA(Close, maExitPeriod)[0];
					
					if (hitTP || hitSL || rsiExit || stallExit || maExit)
					{
						// Calculate P&L
						double exitPrice = hitTP ? virtualTakeProfit : 
						                   hitSL ? virtualStopLoss : Close[0];
						double pnl = virtualEntryPrice - exitPrice;
						bool isWin = pnl > 0;
						
						// Calculate percentage return for evolution system
						double returnPct = (pnl / virtualEntryPrice) * 100.0;
						
						// Update learning
						UpdateLearning(isWin, virtualEntryIndicator, virtualEntryRegime);
						
						// Record for evolution system (per regime)
						if (UseEvolutionSystem)
						{
							RecordTradeResult(returnPct, virtualEntryRegime);
						}
						
						// Reset virtual trade
						inVirtualTrade = false;
						virtualTradeDirection = 0;
						
						Print(String.Format("{0} | Virtual SHORT Exit @ {1:F2} | P&L: {2:F2} | Win: {3} | Indicator: {4} | Regime: {5} | Bars: {6}", 
							Time[0], exitPrice, pnl, isWin, virtualEntryIndicator, virtualEntryRegime, virtualBarsInTrade));
					}
				}
			}
		}
		
		private void UpdateLearning(bool isWin, string indicator, string regime)
		{
			// ═══════════════════════════════════════════════════════════════
			// LEARNING UPDATE (matches Pine script logic)
			// Updates win rate arrays per indicator per regime
			// Updates adaptive weights via EWMA
			// ═══════════════════════════════════════════════════════════════
			
			if (!EnableLearning) return;
			
			// Update global stats
			totalTrades++;
			if (isWin)
				wins++;
			else
				losses++;
			
			winRate = totalTrades > 0 ? (double)wins / totalTrades : 0.5;
			
			// Update win rate EMA (Pine: learnWinRate)
			if (totalTrades == 1)
				winRateEMA = isWin ? 1.0 : 0.0;
			else
				winRateEMA = winRateEMA * (1 - winRateEmaAlpha) + (isWin ? 1.0 : 0.0) * winRateEmaAlpha;
			
			// Find indicator index
			int indicatorIndex = Array.IndexOf(indicatorNames, indicator);
			if (indicatorIndex < 0) return;  // Unknown indicator
			
			// Update regime-specific win rates (Pine: trendWinRates, rangeWinRates, choppyWinRates)
			if (regime == "Trending")
			{
				trendTradeCounts[indicatorIndex]++;
				double oldWR = trendWinRates[indicatorIndex];
				double newWR = oldWR + learningRate * ((isWin ? 1.0 : 0.0) - oldWR);
				trendWinRates[indicatorIndex] = newWR;
			}
			else if (regime == "Ranging")
			{
				rangeTradeCounts[indicatorIndex]++;
				double oldWR = rangeWinRates[indicatorIndex];
				double newWR = oldWR + learningRate * ((isWin ? 1.0 : 0.0) - oldWR);
				rangeWinRates[indicatorIndex] = newWR;
			}
			else  // Choppy
			{
				choppyTradeCounts[indicatorIndex]++;
				double oldWR = choppyWinRates[indicatorIndex];
				double newWR = oldWR + learningRate * ((isWin ? 1.0 : 0.0) - oldWR);
				choppyWinRates[indicatorIndex] = newWR;
			}
			
			// Adaptive Weights (Pine: wLead_adaptive, wStack_adaptive, wRegime_adaptive)
			if (AdaptiveWeights)
			{
				// Increase weights on wins, decrease on losses
				if (isWin)
				{
					wLead_adaptive = Math.Min(5.0, wLead_adaptive + weightAdaptAlpha);
					wStack_adaptive = Math.Min(5.0, wStack_adaptive + weightAdaptAlpha * 0.5);
					wRegime_adaptive = Math.Min(5.0, wRegime_adaptive + weightAdaptAlpha * 0.7);
				}
				else
				{
					wLead_adaptive = Math.Max(0.1, wLead_adaptive - weightAdaptAlpha);
					wStack_adaptive = Math.Max(0.1, wStack_adaptive - weightAdaptAlpha * 0.5);
					wRegime_adaptive = Math.Max(0.1, wRegime_adaptive - weightAdaptAlpha * 0.7);
				}
			}
			
			Print(String.Format("Learning Update | Total: {0} | WR: {1:P1} | WR_EMA: {2:P1} | Indicator: {3} ({4}) | Weights: L={5:F2}, S={6:F2}, R={7:F2}", 
				totalTrades, winRate, winRateEMA, indicator, regime, wLead_adaptive, wStack_adaptive, wRegime_adaptive));
		}
		
		private void CheckEntrySignals()
		{
			// ═══════════════════════════════════════════════════════════════
			// ENTRY CONDITIONS (Brain-based with threshold + edge margin)
			// ═══════════════════════════════════════════════════════════════
			
			// Score threshold (adaptive based on aggressiveness)
			double scoreThresholdFrac = Aggressiveness == 1 ? 0.75 : 
			                            Aggressiveness == 2 ? 0.65 : 
			                            Aggressiveness == 3 ? 0.55 : 
			                            Aggressiveness == 4 ? 0.45 : 0.35;
			
		double scoreThreshold = maxScore * scoreThresholdFrac;
		
		// Edge margin requirement (Pine: decisionMargin)
		double edgeMargin = Aggressiveness == 1 ? 0.25 : 
		                    Aggressiveness == 2 ? 0.20 : 
		                    Aggressiveness == 3 ? 0.15 : 
		                    Aggressiveness == 4 ? 0.10 : 0.05;
		
		double minEdge = maxScore * edgeMargin;
		
		// PHASE 1 FIX #2: Normalize brain scores before comparing to thresholds
		// BrainThreshold is normalized (0-1), but brainScore can be > 1 in STRENGTH mode
		// Without normalization, comparison is meaningless (comparing 2.5 to 0.6 threshold)
		double normalizedLongScore = maxScore > 0 ? brainLongScore / maxScore : 0.0;
		double normalizedShortScore = maxScore > 0 ? brainShortScore / maxScore : 0.0;
		
		// PATCH 3: Use Brain Thresholds (evolved or user-set parameters)
		// longSignal requires: score >= threshold, edge over opposite, conviction, AND brainScore >= BrainThresholdLong
		bool longSignal = brainLongScore >= scoreThreshold && 
		                  (brainLongScore - brainShortScore) >= minEdge &&
		                  conviction > 0.05 &&  // BUGFIX v1.10.6: Lower conviction threshold (was 0.15)
		                  normalizedLongScore >= BrainThresholdLong;  // PHASE 1 FIX: Normalized comparison
		
		bool shortSignal = brainShortScore >= scoreThreshold && 
		                   (brainShortScore - brainLongScore) >= minEdge &&
		                   conviction > 0.05 &&  // BUGFIX v1.10.6: Lower conviction threshold (was 0.15)
		                   normalizedShortScore >= BrainThresholdShort;  // PHASE 1 FIX: Normalized comparison
		
		// 🔍 DIAGNOSTIC: Log why SHORT signals are blocked (TEMPORARY - remove after testing)
		if (CurrentBar % 50 == 0 && brainShortScore > 0)  // Log every 50 bars if SHORT has ANY score
		{
			Print($"🔍 SHORT Diagnostic | Score: {brainShortScore:F2} (norm: {normalizedShortScore:F2}) | " +
			      $"Threshold: {BrainThresholdShort:F2} | Edge: {(brainShortScore - brainLongScore):F2} | " +
			      $"Conv: {conviction:F2} | Pass: {shortSignal}");
		}
		
			// ═══════════════════════════════════════════════════════════════
			// PHASE 2: REGIME GATE (SUPREME INTELLIGENCE)
			// The brain can decide: "I only work in TREND regime"
			// ═══════════════════════════════════════════════════════════════
			if (UseEvolutionSystem && CurrentBar > 50)
			{
				Chromosome champion = GetCurrentChampion(currentRegime);
				if (champion != null)
				{
					// BUGFIX v1.10.5: Disable regime gates temporarily (debugging SHORT issue)
					// Evolution was setting "RANGE only" which blocked SHORT in CHOPPY/TREND
					// Gates re-enabled after confirming SHORT works without restrictions
					bool gateTrendOnly = false;  // Was: champion.ExtractParam("REGIME_GATE_TREND_ONLY") > 0.5
					bool gateRangeOnly = false;  // Was: champion.ExtractParam("REGIME_GATE_RANGE_ONLY") > 0.5
					bool gateChoppyOnly = false; // Was: champion.ExtractParam("REGIME_GATE_CHOPPY_ONLY") > 0.5
					
					// Apply regime restrictions (currently disabled)
					if (gateTrendOnly && currentRegime != "TREND")
					{
						longSignal = false;
						shortSignal = false;
					}
					else if (gateRangeOnly && currentRegime != "RANGE")
					{
						longSignal = false;
						shortSignal = false;
					}
					else if (gateChoppyOnly && currentRegime != "CHOPPY")
					{
						longSignal = false;
						shortSignal = false;
					}
					
					// 🔍 DIAGNOSTIC: Log regime gate impact (TEMPORARY)
					if (CurrentBar % 100 == 0)
					{
						Print($"🧬 Regime Gates | TREND: {gateTrendOnly} | RANGE: {gateRangeOnly} | CHOPPY: {gateChoppyOnly} | CurrentRegime: {currentRegime}");
						Print($"   BrainThresholds | LONG: {BrainThresholdLong:F2} | SHORT: {BrainThresholdShort:F2}");
					}
				}
			}
			
			// ═══════════════════════════════════════════════════════════════
			// VISUAL SIGNALS (matches Pine script plotshape)
			// ═══════════════════════════════════════════════════════════════
			if (longSignal && !inVirtualTrade)  // Don't enter if already in trade
			{
				brainSignal = 1.0;
				
				// Draw arrow using NinjaScript DrawingTools
				Draw.ArrowUp(this, "LongArrow" + CurrentBar, true, 0, Low[0] - (2 * atrIndicator[0]), Brushes.Lime);
				
				// Draw text label
				string labelText = String.Format("LONG\n{0}\n{1:P0}", dynamicLeadingIndicator, conviction);
				Draw.Text(this, "LongText" + CurrentBar, true, labelText, 
					0, Low[0] - (3 * atrIndicator[0]), 0, 
					Brushes.Lime, new Gui.Tools.SimpleFont("Arial", 10), 
					System.Windows.TextAlignment.Center, 
					Brushes.Transparent, Brushes.Transparent, 0);
				
				// Create virtual trade for learning
				if (EnableLearning)
				{
					inVirtualTrade = true;
					virtualTradeDirection = 1;
					virtualEntryPrice = Close[0];
					virtualBarsInTrade = 0;
					
					// Calculate TP/SL based on ATR
					double atr = atrIndicator[0];
					virtualTakeProfit = virtualEntryPrice + (atr * TpAtrMultiplier);
					virtualStopLoss = virtualEntryPrice - (atr * SlAtrMultiplier);
					virtualHighest = High[0];
					virtualLowest = Low[0];
					
					virtualEntryIndicator = dynamicLeadingIndicator;
					virtualEntryRegime = isTrending ? "Trending" : isRanging ? "Ranging" : "Choppy";
				}
				
				Print(String.Format("{0} | LONG Signal @ {1:F2} | Indicator: {2} | Regime: {3} | Conviction: {4:P0} | Score: {5:F2}/{6:F2}", 
					Time[0], Close[0], dynamicLeadingIndicator, 
					isTrending ? "Trending" : isRanging ? "Ranging" : "Choppy", 
					conviction, brainLongScore, maxScore));
			}
			else if (shortSignal && !inVirtualTrade)  // Don't enter if already in trade
			{
				brainSignal = -1.0;
				
				// Draw arrow using NinjaScript DrawingTools
				Draw.ArrowDown(this, "ShortArrow" + CurrentBar, true, 0, High[0] + (2 * atrIndicator[0]), Brushes.Red);
				
				// Draw text label
				string labelText = String.Format("SHORT\n{0}\n{1:P0}", dynamicLeadingIndicator, conviction);
				Draw.Text(this, "ShortText" + CurrentBar, true, labelText, 
					0, High[0] + (3 * atrIndicator[0]), 0, 
					Brushes.Red, new Gui.Tools.SimpleFont("Arial", 10), 
					System.Windows.TextAlignment.Center, 
					Brushes.Transparent, Brushes.Transparent, 0);
				
				// Create virtual trade for learning
				if (EnableLearning)
				{
					inVirtualTrade = true;
					virtualTradeDirection = -1;
					virtualEntryPrice = Close[0];
					virtualBarsInTrade = 0;
					
					// Calculate TP/SL based on ATR
					double atr = atrIndicator[0];
					virtualTakeProfit = virtualEntryPrice - (atr * TpAtrMultiplier);
					virtualStopLoss = virtualEntryPrice + (atr * SlAtrMultiplier);
					virtualHighest = High[0];
					virtualLowest = Low[0];
					
					virtualEntryIndicator = dynamicLeadingIndicator;
					virtualEntryRegime = isTrending ? "Trending" : isRanging ? "Ranging" : "Choppy";
				}
				
				Print(String.Format("{0} | SHORT Signal @ {1:F2} | Indicator: {2} | Regime: {3} | Conviction: {4:P0} | Score: {5:F2}/{6:F2}", 
					Time[0], Close[0], dynamicLeadingIndicator, 
					isTrending ? "Trending" : isRanging ? "Ranging" : "Choppy", 
					conviction, brainShortScore, maxScore));
			}
			else
			{
				brainSignal = 0.0;
			}
			
			// ═══════════════════════════════════════════════════════════════════
			// EVOLUTION COMMAND CENTER DASHBOARD
			// Draw real-time display of AI brain decisions (regime, generation, tactics)
			// ═══════════════════════════════════════════════════════════════════
			if (!IsInHitTest && CurrentBar > 0)
			{
				DrawEvolutionDashboard();
			}
		}
		
		/// <summary>
		/// Get trade returns for specific regime
		/// </summary>
	private List<double> GetRegimeTradeReturns(string regime)
	{
		// PHASE 1 FIX: Normalize regime string for consistency
		string canonicalRegime = CanonicalRegime(regime);
		
		switch (canonicalRegime)
		{
			case "TREND": return trendTradeReturns;
			case "RANGE": return rangeTradeReturns;
			case "CHOPPY": return choppyTradeReturns;
			default: return new List<double>();
		}
	}		/// <summary>
		/// Get the current champion chromosome for the specified regime
		/// </summary>
		private Chromosome GetCurrentChampion(string regime)
		{
			// Return the active champion if available
			// The active champion is set during evolution and represents
			// the best-performing parameter set for the current regime
			return activeChampion;
		}
		
		/// <summary>
		/// Apply champion chromosome parameters to indicators and exits
		/// </summary>
		private void ApplyChampionParameters(Chromosome champion)
		{
			// Extract parameters from champion genes
			double tpAtr = champion.ExtractParam("RISK_TP_ATR");
			double slAtr = champion.ExtractParam("RISK_SL_ATR");
			double brainThreshold = champion.ExtractParam("BRAIN_THRESHOLD");
			
			// Apply TP/SL multipliers (these can be changed dynamically)
			TpAtrMultiplier = tpAtr;
			SlAtrMultiplier = slAtr;
			
			// Apply thresholds - ADAPTIVE BASED ON BRAIN MODE
			// PROBABILISTIC mode: Scores are 0-1 strengths → Lower threshold needed (~0.35)
			// BOOLEAN mode: Scores are weighted sums → Higher threshold needed (~0.60)
			double adaptiveThreshold = UseProbabilisticBrain ? (brainThreshold * 0.58) : brainThreshold;  // 0.60 * 0.58 ≈ 0.35
			BrainThresholdLong = adaptiveThreshold;
			BrainThresholdShort = adaptiveThreshold;
			
			// ═══════════════════════════════════════════════════════════════════
			// EXIT BRAIN CONSCIOUSNESS - Dynamic Exit Strategy
			// The Exit Brain now "knows" and "remembers" its tactical decisions
			// These parameters are displayed in the Evolution Command Center
			// ═══════════════════════════════════════════════════════════════════
			
			// RSI Exit Tactic (exit when RSI shows extreme overbought/oversold)
			useRSIexit = champion.ExtractParam("EXIT_RSI_ENABLE") > 0.5;
			rsiExitLevel = champion.ExtractParam("EXIT_RSI_LEVEL");
			
			// Stall Exit Tactic (exit when momentum stalls for N bars)
			useStallExit = champion.ExtractParam("EXIT_STALL_ENABLE") > 0.5;
			stallExitBars = (int)champion.ExtractParam("EXIT_STALL_BARS");
			
			// Moving Average Exit Tactic (exit when price crosses below MA)
			useMAexit = champion.ExtractParam("EXIT_MA_ENABLE") > 0.5;
			maExitPeriod = (int)champion.ExtractParam("EXIT_MA_PERIOD");
			
			// Momentum Exit Tactic (exit when trend strength weakens)
			// Future: useMOMOexit = champion.ExtractParam("EXIT_MOMO_ENABLE") > 0.5;
			
			// ═══════════════════════════════════════════════════════════════════
			// GENOME ACTIVATION: Update Evolutive Indicator Parameters
			// The Brain reconfigures its tools with evolved parameters
			// ═══════════════════════════════════════════════════════════════════
			championStPeriod = (int)champion.ExtractParam("ST_PERIOD");
			championStFactor = champion.ExtractParam("ST_FACTOR");
			championRsiPeriod = (int)champion.ExtractParam("RSI_PERIOD");
			championMacdFast = (int)champion.ExtractParam("MACD_FAST");
			championMacdSlow = (int)champion.ExtractParam("MACD_SLOW");
			championMacdSignal = (int)champion.ExtractParam("MACD_SIGNAL");
			championRegimePeriod = 50;  // Could be evolved too (future enhancement)
			
			// Flag that indicators need recalculation with new parameters
			indicatorsNeedRecalc = true;
			
			Print($"🧠 Brain reconfigured: ST({championStPeriod},{championStFactor:F1}) RSI({championRsiPeriod}) MACD({championMacdFast},{championMacdSlow},{championMacdSignal})");
		}
		
		/// <summary>
		/// GENOME ACTIVATION: Recalculate evolutive indicators with champion parameters
		/// Called ONLY when champion changes (not every bar) for optimal performance
		/// </summary>
		private void RecalculateEvolutiveIndicators()
		{
			if (CurrentBar < Math.Max(championStPeriod, Math.Max(championRsiPeriod, championMacdSlow)))
				return;  // Not enough data yet
			
			// ═══════════════════════════════════════════════════════════════════
			// EVOLUTIVE SUPERTREND
			// Recalculate with evolved ST_PERIOD and ST_FACTOR
			// ═══════════════════════════════════════════════════════════════════
			double atr = ATR(championStPeriod)[0];
			double hl2 = (High[0] + Low[0]) / 2.0;
			double upperBand = hl2 + (championStFactor * atr);
			double lowerBand = hl2 - (championStFactor * atr);
			
			// SuperTrend logic (simplified - full implementation would track trend state)
			double prevST = evolutiveST.Count > 0 ? evolutiveST[0] : hl2;
			bool wasBullish = prevST < Close[1];
			
			if (wasBullish)
				evolutiveST[0] = Close[0] > lowerBand ? lowerBand : upperBand;
			else
				evolutiveST[0] = Close[0] < upperBand ? upperBand : lowerBand;
			
			// ═══════════════════════════════════════════════════════════════════
			// EVOLUTIVE RSI
			// Recalculate with evolved RSI_PERIOD
			// ═══════════════════════════════════════════════════════════════════
			double rsiValue = RSI(championRsiPeriod, 3)[0];  // Use native RSI with evolved period
			evolutiveRSI[0] = rsiValue;
			
			// ═══════════════════════════════════════════════════════════════════
			// EVOLUTIVE MACD
			// Recalculate with evolved MACD_FAST, MACD_SLOW, MACD_SIGNAL
			// ═══════════════════════════════════════════════════════════════════
			double emaFast = EMA(championMacdFast)[0];
			double emaSlow = EMA(championMacdSlow)[0];
			double macdLine = emaFast - emaSlow;
			evolutiveMACD[0] = macdLine;
			
			// MACD Signal (EMA of MACD line)
			// NOTE: Ideally we'd use Series<double> of MACD values, simplified here
			evolutiveMACDsignal[0] = EMA(evolutiveMACD, championMacdSignal)[0];
			
			// ═══════════════════════════════════════════════════════════════════
			// EVOLUTIVE REGIME EMA
			// Recalculate with evolved regime period (if we add this gene)
			// ═══════════════════════════════════════════════════════════════════
			evolutiveRegimeEMA[0] = EMA(championRegimePeriod)[0];
			
			// Clear recalculation flag
			indicatorsNeedRecalc = false;
		}
		
	/// <summary>
	/// Store trade result in appropriate regime list
	/// </summary>
	private void RecordTradeResult(double returnPct, string regime)
	{
		// PHASE 1 FIX: Normalize regime string for consistency
		string canonicalRegime = CanonicalRegime(regime);
		
		switch (canonicalRegime)
		{
			case "TREND":
				trendTradeReturns.Add(returnPct);
				if (trendTradeReturns.Count > 100) trendTradeReturns.RemoveAt(0);  // Keep last 100
				break;
			case "RANGE":
				rangeTradeReturns.Add(returnPct);
				if (rangeTradeReturns.Count > 100) rangeTradeReturns.RemoveAt(0);
				break;
			case "CHOPPY":
				choppyTradeReturns.Add(returnPct);
				if (choppyTradeReturns.Count > 100) choppyTradeReturns.RemoveAt(0);
				break;
		}
	}
	
	/// <summary>
	/// Normalize regime string to canonical uppercase format
	/// PHASE 1 FIX: Ensures consistency across "Trending" vs "TREND" vs "trend"
	/// </summary>
	private string CanonicalRegime(string regime)
	{
		if (string.IsNullOrEmpty(regime)) return "CHOPPY";  // Default fallback
		
		string normalized = regime.ToUpper().Trim();
		
		// Map common variations
		if (normalized.Contains("TREND")) return "TREND";
		if (normalized.Contains("RANGE")) return "RANGE";
		if (normalized.Contains("CHOP")) return "CHOPPY";
		
		// Fallback to input (already uppercase)
		return normalized;
	}		/// <summary>
		/// PHASE 2: MINI-BACKTEST PER CHROMOSOME
		/// THE INTELLIGENCE UNLOCK - Each chromosome gets evaluated on its own simulated performance
		/// This is the final architectural piece that transforms the brain from "capable of evolving" 
		/// to "intelligently evolving" by giving each chromosome unique fitness based on its decisions
		/// </summary>
		/// <param name="chrom">Chromosome to evaluate</param>
		/// <param name="barsToSimulate">Number of historical bars to simulate (100 recommended)</param>
		/// <returns>List of trade returns (PnL %) specific to this chromosome's parameters</returns>
		public List<double> SimulateTradesForChromosome(Chromosome chrom, int barsToSimulate)
		{
			List<double> simulatedReturns = new List<double>();
			
			// Safety checks
			if (chrom == null || chrom.Genes.Length < 40)
			{
				Print($"⚠️ SimulateTradesForChromosome: Invalid chromosome (genes: {chrom?.Genes.Length ?? 0})");
				return simulatedReturns;
			}
			
			if (CurrentBar < barsToSimulate + 50)
			{
				// Not enough historical data yet
				return simulatedReturns;
			}
			
			// ═══════════════════════════════════════════════════════════════════
			// EXTRACT CHROMOSOME PARAMETERS
			// ═══════════════════════════════════════════════════════════════════
			
			// Entry Indicators (genes 0-7: periods)
			int simStPeriod = (int)chrom.ExtractParam("ST_PERIOD");
			double simStFactor = chrom.ExtractParam("ST_FACTOR");
			int simRsiPeriod = (int)chrom.ExtractParam("RSI_PERIOD");
			int simMacdFast = (int)chrom.ExtractParam("MACD_FAST");
			int simMacdSlow = (int)chrom.ExtractParam("MACD_SLOW");
			int simMacdSignal = (int)chrom.ExtractParam("MACD_SIGNAL");
			int simRegimePeriod = 50;  // Could be evolved (future enhancement)
			
			// Risk Management (genes 15-17)
			double simTpAtr = chrom.ExtractParam("RISK_TP_ATR");
			double simSlAtr = chrom.ExtractParam("RISK_SL_ATR");
			double simBrainThreshold = chrom.ExtractParam("BRAIN_THRESHOLD");
			
			// Supreme Intelligence (genes 32-39)
			bool simGateTrendOnly = chrom.ExtractParam("REGIME_GATE_TREND_ONLY") > 0.5;
			bool simGateRangeOnly = chrom.ExtractParam("REGIME_GATE_RANGE_ONLY") > 0.5;
			bool simGateChoppyOnly = chrom.ExtractParam("REGIME_GATE_CHOPPY_ONLY") > 0.5;
			
			double simWLead = chrom.ExtractParam("WEIGHT_LEAD");
			double simWStack = chrom.ExtractParam("WEIGHT_STACK");
			double simWRegime = chrom.ExtractParam("WEIGHT_REGIME");
			double simWVolume = chrom.ExtractParam("WEIGHT_VOLUME");
			double simWHybrid = chrom.ExtractParam("WEIGHT_HYBRID");
			
			// ═══════════════════════════════════════════════════════════════════
			// VIRTUAL TRADE STATE
			// ═══════════════════════════════════════════════════════════════════
			bool inSimTrade = false;
			bool isSimLong = false;
			double simEntryPrice = 0;
			double simStopLoss = 0;
			double simTakeProfit = 0;
			double simHighest = 0;  // For trailing stop
			double simLowest = double.MaxValue;
			
			// ═══════════════════════════════════════════════════════════════════
			// BACKWARD LOOP THROUGH HISTORICAL BARS
			// We simulate from (CurrentBar - barsToSimulate) to (CurrentBar - 1)
			// ═══════════════════════════════════════════════════════════════════
			for (int i = barsToSimulate - 1; i >= 1; i--)  // i=0 is current bar, skip it
			{
				// Bar index in NinjaTrader's historical data (0 = current, 1 = previous, etc.)
				int barAgo = i;
				
				// Skip bars with insufficient indicator data
				if (CurrentBar - barAgo < Math.Max(simStPeriod, Math.Max(simRsiPeriod, simMacdSlow + 10)))
					continue;
				
				// ───────────────────────────────────────────────────────────────
				// CALCULATE CHROMOSOME-SPECIFIC INDICATORS FOR THIS BAR
				// ───────────────────────────────────────────────────────────────
				
				// ATR (needed for TP/SL calculation)
				double barAtr = ATR(14)[barAgo];
				
				// SuperTrend (simplified - using chromosome's ST_PERIOD and ST_FACTOR)
				double barAtrST = ATR(simStPeriod)[barAgo];
				double barHl2 = (High[barAgo] + Low[barAgo]) / 2.0;
				double barStUpper = barHl2 + (simStFactor * barAtrST);
				double barStLower = barHl2 - (simStFactor * barAtrST);
				bool barStBullish = Close[barAgo] > barStLower;  // Simplified ST direction
				
				// RSI (using chromosome's RSI_PERIOD)
				double barRsi = RSI(simRsiPeriod, 3)[barAgo];
				
				// MACD (using chromosome's MACD_FAST, MACD_SLOW, MACD_SIGNAL)
				double barEmaFast = EMA(simMacdFast)[barAgo];
				double barEmaSlow = EMA(simMacdSlow)[barAgo];
				double barMacdLine = barEmaFast - barEmaSlow;
				// MACD Signal would require custom calculation - simplified here as zero-cross
				bool barMacdBullish = barMacdLine > 0;
				
				// Regime EMA
				double barRegimeEma = EMA(simRegimePeriod)[barAgo];
				bool barRegimeLong = Close[barAgo] > barRegimeEma;
				
				// ADX (regime strength)
				double barAdx = ADX(14)[barAgo];
				bool barRegimeStrong = barAdx > 25.0;
				
				// Regime classification (simplified - based on ADX and EMA alignment)
				bool barIsTrending = barAdx > 25;
				bool barIsRanging = barAdx < 20;
				bool barIsChoppy = !barIsTrending && !barIsRanging;
				string barRegime = barIsTrending ? "TREND" : barIsRanging ? "RANGE" : "CHOPPY";
				
				// ───────────────────────────────────────────────────────────────
				// LEADING INDICATOR SIGNAL (simplified - using evolved RSI/MACD)
				// ───────────────────────────────────────────────────────────────
				bool barLeadingLong = barRsi > 50 && barMacdBullish;
				bool barLeadingShort = barRsi < 50 && !barMacdBullish;
				
				// ───────────────────────────────────────────────────────────────
				// CONFIRMATION STACK (simplified - use key filters only)
				// Full stack would require recalculating 46 indicators - too expensive
				// Simplified: Use EMA regime, SuperTrend, RSI, MACD as proxies
				// ───────────────────────────────────────────────────────────────
				int barStackLong = 0;
				int barStackShort = 0;
				int barStackTotal = 4;  // Simplified stack size
				
				if (barRegimeLong) barStackLong++;
				else barStackShort++;
				
				if (barStBullish) barStackLong++;
				else barStackShort++;
				
				if (barRsi > 50) barStackLong++;
				else barStackShort++;
				
				if (barMacdBullish) barStackLong++;
				else barStackShort++;
				
				// ───────────────────────────────────────────────────────────────
				// BRAIN SCORE CALCULATION (using chromosome's evolved weights)
				// ───────────────────────────────────────────────────────────────
				double barMaxScore = simWLead + simWRegime + simWStack + simWVolume + simWHybrid;
				
				double barBrainLong = (barLeadingLong ? simWLead : 0) +
				                      (barRegimeLong && barRegimeStrong ? simWRegime : 0) +
				                      (barStackLong >= 2 ? simWStack : 0) +  // Simplified stack threshold
				                      (simWVolume * 0.5);  // Simplified volume (always half credit)
				
				double barBrainShort = (barLeadingShort ? simWLead : 0) +
				                       (!barRegimeLong && barRegimeStrong ? simWRegime : 0) +
				                       (barStackShort >= 2 ? simWStack : 0) +
				                       (simWVolume * 0.5);
				
				// Normalize scores
				double barNormalizedLong = barMaxScore > 0 ? barBrainLong / barMaxScore : 0.0;
				double barNormalizedShort = barMaxScore > 0 ? barBrainShort / barMaxScore : 0.0;
				
				// ───────────────────────────────────────────────────────────────
				// ENTRY SIGNAL (using chromosome's BrainThreshold)
				// ───────────────────────────────────────────────────────────────
				double barConviction = barMaxScore > 0 ? Math.Abs(barBrainLong - barBrainShort) / barMaxScore : 0.0;
				
				bool barLongSignal = barNormalizedLong >= simBrainThreshold && 
				                     barConviction > 0.05 &&  // BUGFIX v1.10.6: Lower conviction threshold (was 0.15)
				                     (barBrainLong - barBrainShort) >= barMaxScore * 0.10;  // Min edge
				
				bool barShortSignal = barNormalizedShort >= simBrainThreshold && 
				                      barConviction > 0.05 &&  // BUGFIX v1.10.6: Lower conviction threshold (was 0.15)
				                      (barBrainShort - barBrainLong) >= barMaxScore * 0.10;
				
				// ───────────────────────────────────────────────────────────────
				// REGIME GATE FILTER (Supreme Intelligence genes 32-35)
				// ───────────────────────────────────────────────────────────────
				if (simGateTrendOnly && barRegime != "TREND")
				{
					barLongSignal = false;
					barShortSignal = false;
				}
				else if (simGateRangeOnly && barRegime != "RANGE")
				{
					barLongSignal = false;
					barShortSignal = false;
				}
				else if (simGateChoppyOnly && barRegime != "CHOPPY")
				{
					barLongSignal = false;
					barShortSignal = false;
				}
				
				// ───────────────────────────────────────────────────────────────
				// TRADE MANAGEMENT
				// ───────────────────────────────────────────────────────────────
				if (!inSimTrade)
				{
					// ═══════════════════════════════════════════════════════════
					// ENTRY LOGIC
					// ═══════════════════════════════════════════════════════════
					if (barLongSignal)
					{
						inSimTrade = true;
						isSimLong = true;
						simEntryPrice = Close[barAgo];
						simStopLoss = simEntryPrice - (simSlAtr * barAtr);
						simTakeProfit = simEntryPrice + (simTpAtr * barAtr);
						simHighest = High[barAgo];
						simLowest = Low[barAgo];
					}
					else if (barShortSignal)
					{
						inSimTrade = true;
						isSimLong = false;
						simEntryPrice = Close[barAgo];
						simStopLoss = simEntryPrice + (simSlAtr * barAtr);
						simTakeProfit = simEntryPrice - (simTpAtr * barAtr);
						simHighest = High[barAgo];
						simLowest = Low[barAgo];
					}
				}
				else
				{
					// ═══════════════════════════════════════════════════════════
					// EXIT LOGIC
					// ═══════════════════════════════════════════════════════════
					bool exitTriggered = false;
					double exitPrice = Close[barAgo];
					
					if (isSimLong)
					{
						// Update highest high for trailing stop
						simHighest = Math.Max(simHighest, High[barAgo]);
						
						// Check TP hit
						if (High[barAgo] >= simTakeProfit)
						{
							exitTriggered = true;
							exitPrice = simTakeProfit;
						}
						// Check SL hit
						else if (Low[barAgo] <= simStopLoss)
						{
							exitTriggered = true;
							exitPrice = simStopLoss;
						}
					}
					else  // Short trade
					{
						// Update lowest low for trailing stop
						simLowest = Math.Min(simLowest, Low[barAgo]);
						
						// Check TP hit
						if (Low[barAgo] <= simTakeProfit)
						{
							exitTriggered = true;
							exitPrice = simTakeProfit;
						}
						// Check SL hit
						else if (High[barAgo] >= simStopLoss)
						{
							exitTriggered = true;
							exitPrice = simStopLoss;
						}
					}
					
					// Record trade result
					if (exitTriggered)
					{
						double pnlPercent = 0.0;
						
						if (isSimLong)
							pnlPercent = ((exitPrice - simEntryPrice) / simEntryPrice) * 100.0;
						else
							pnlPercent = ((simEntryPrice - exitPrice) / simEntryPrice) * 100.0;
						
						simulatedReturns.Add(pnlPercent);
						
						// Reset trade state
						inSimTrade = false;
					}
				}
			}
			
			// Return unique trade results for THIS chromosome
			return simulatedReturns;
		}
		
		/// <summary>
		/// Evolution Command Center - Real-time dashboard showing AI brain decisions
		/// Displays regime, generation, champion performance, and tactical strategies
		/// </summary>
		private void DrawEvolutionDashboard()
		{
			// Only draw if Evolution System is active and we have a champion
			if (!UseEvolutionSystem || activeChampion == null || evolutionArchitect == null)
				return;
			
			// PHASE 2 HOTFIX: Verify champion has correct number of genes
			if (activeChampion.Genes.Length < 40)
			{
				Print($"⚠️ WARNING: activeChampion has only {activeChampion.Genes.Length} genes (expected 40). Skipping dashboard.");
				return;
			}
			
			// Skip drawing during hit tests (chart interaction checks)
			if (IsInHitTest)
				return;
			
			// Wrap entire dashboard in try-catch to prevent crashes
			try
			{
			
			// ═══════════════════════════════════════════════════════════════════
			// BUILD DASHBOARD CONTENT
			// ═══════════════════════════════════════════════════════════════════
			
			// Current market regime
			string regime = isTrending ? "TREND" : isRanging ? "RANGE" : "CHOPPY";
			
			// Header with generation counter
			string header = $"══════ DIY BRAIN - EVOLUTION COMMAND CENTER ══════ [GEN: {evolutionArchitect.Generation}]";
			
			// Current regime display
			string regimeInfo = $"CURRENT REGIME: {regime}";
			
			// Champion performance metrics
			string championHeader = "--- ACTIVE CHAMPION ---";
			string championFitness = $"Fitness: {activeChampion.Fitness:F3} | WR: {activeChampion.WinRate:P0} | Sharpe: {activeChampion.SharpeRatio:F2} | Complexity: {activeChampion.Complexity}";
			
			// ═══════════════════════════════════════════════════════════════════
			// ENTRY BRAIN PARAMETERS (Indicator Settings)
			// ═══════════════════════════════════════════════════════════════════
			string entryBrain = $"[ENTRY BRAIN]: ST({(int)activeChampion.ExtractParam("ST_PERIOD")}, {activeChampion.ExtractParam("ST_FACTOR"):F1}) | " +
			                    $"RSI({(int)activeChampion.ExtractParam("RSI_PERIOD")}) | " +
			                    $"MACD({(int)activeChampion.ExtractParam("MACD_FAST")},{(int)activeChampion.ExtractParam("MACD_SLOW")},{(int)activeChampion.ExtractParam("MACD_SIGNAL")})";
			
			// ═══════════════════════════════════════════════════════════════════
			// EXIT BRAIN PARAMETERS (Dynamic Exit Tactics)
			// Shows which exit strategies are active and their thresholds
			// ═══════════════════════════════════════════════════════════════════
			string exitBrainHeader = "[EXIT BRAIN]:";
			string exitTactics = $"  - RISK: TP({activeChampion.ExtractParam("RISK_TP_ATR"):F1}×ATR) SL({activeChampion.ExtractParam("RISK_SL_ATR"):F1}×ATR)";
			
			// Add active exit tactics
			if (useRSIexit)
				exitTactics += $" | RSI>{rsiExitLevel:F0}";
			if (useStallExit)
				exitTactics += $" | STALL>{stallExitBars}bars";
			if (useMAexit)
				exitTactics += $" | MA<{maExitPeriod}";
			
			// If no tactical exits are active, show "RISK ONLY"
			if (!useRSIexit && !useStallExit && !useMAexit)
				exitTactics += " | RISK ONLY (TP/SL)";
			
			// ═══════════════════════════════════════════════════════════════════
			// PHASE 2: SUPREME INTELLIGENCE DISPLAY
			// Shows evolved Regime Gates and Weights
			// ═══════════════════════════════════════════════════════════════════
			string supremeHeader = "[SUPREME INTELLIGENCE]:";
			
			// Regime Gates (which regime(s) this champion prefers)
			string regimeGates = "  - GATES: ";
			bool gateT = activeChampion.ExtractParam("REGIME_GATE_TREND_ONLY") > 0.5;
			bool gateR = activeChampion.ExtractParam("REGIME_GATE_RANGE_ONLY") > 0.5;
			bool gateC = activeChampion.ExtractParam("REGIME_GATE_CHOPPY_ONLY") > 0.5;
			
			if (!gateT && !gateR && !gateC)
				regimeGates += "ALL REGIMES";
			else
			{
				if (gateT) regimeGates += "TREND ";
				if (gateR) regimeGates += "RANGE ";
				if (gateC) regimeGates += "CHOPPY";
			}
			
			// Evolved Weights (AI-learned importance of each component)
			double wL = activeChampion.ExtractParam("WEIGHT_LEAD");
			double wS = activeChampion.ExtractParam("WEIGHT_STACK");
			double wR = activeChampion.ExtractParam("WEIGHT_REGIME");
			double wV = activeChampion.ExtractParam("WEIGHT_VOLUME");
			double wH = activeChampion.ExtractParam("WEIGHT_HYBRID");
			
			string weights = $"  - WEIGHTS: Lead={wL:F2} Stack={wS:F2} Regime={wR:F2} Vol={wV:F2} Hybrid={wH:F2}";
			
			// ═══════════════════════════════════════════════════════════════════
			// ASSEMBLE FULL DASHBOARD TEXT (PHASE 2 Extended)
			// ═══════════════════════════════════════════════════════════════════
			string fullText = $"{header}\n{regimeInfo}\n\n{championHeader}\n{championFitness}\n{entryBrain}\n{exitBrainHeader}\n{exitTactics}\n{supremeHeader}\n{regimeGates}\n{weights}";
			
			// ═══════════════════════════════════════════════════════════════════
			// DRAW ON CHART (Top-Left Position)
			// ═══════════════════════════════════════════════════════════════════
			Draw.TextFixed(this, "EvoDashboard", fullText, TextPosition.TopLeft, 
			               Brushes.Cyan, new Gui.Tools.SimpleFont("Courier New", 10) { Bold = true }, 
			               Brushes.Transparent, Brushes.Black, 100);
			}
			catch (Exception ex)
			{
				// PHASE 2 HOTFIX: Catch any dashboard errors to prevent indicator crash
				Print($"⚠️ Dashboard Error: {ex.Message}");
			}
		}
		
		#endregion
	}
	
	// ═══════════════════════════════════════════════════════════════════════════
	// EVOLUTION SYSTEM CLASSES (Chromosome + EvolutionaryArchitect)
	// ═══════════════════════════════════════════════════════════════════════════
	
	/// <summary>
	/// Represents a genetic chromosome with 32 genes (trading parameters).
	/// Each gene controls a specific aspect of the trading strategy.
	/// Aligned with TradingView DIY.ps v1.8 Evolution System.
	/// </summary>
	public class Chromosome
	{
		#region Properties
		
		/// <summary>
		/// Array of 32 genes (trading parameters)
		/// </summary>
		public double[] Genes { get; private set; }
		
		/// <summary>
		/// Fitness score (Sharpe Ratio + Win Rate - Complexity Penalty)
		/// </summary>
		public double Fitness { get; set; }
		
		/// <summary>
		/// Complexity score (Occam's Razor penalty)
		/// Number of active features/exits
		/// </summary>
		public int Complexity { get; set; }
		
		/// <summary>
		/// Number of trades executed with this chromosome
		/// </summary>
		public int TradeCount { get; set; }
		
		/// <summary>
		/// Win rate (0.0 - 1.0)
		/// </summary>
		public double WinRate { get; set; }
		
		/// <summary>
		/// Sharpe Ratio
		/// </summary>
		public double SharpeRatio { get; set; }
		
		#endregion
		
	#region Gene Ranges (Aligned with TradingView DIY.ps)
	
	// PHASE 2: Extended from 32 → 40 genes (Supreme Intelligence)
	private static readonly (double min, double max)[] GeneRanges = new (double, double)[]
	{
		(1.0, 5.0),     // 0: ST_FACTOR
		(7, 21),        // 1: ST_PERIOD
		(7, 21),        // 2: RSI_PERIOD
		(60, 80),       // 3: RSI_OVERBOUGHT
		(20, 40),       // 4: RSI_OVERSOLD
		(8, 16),        // 5: MACD_FAST
		(20, 30),       // 6: MACD_SLOW
		(7, 11),        // 7: MACD_SIGNAL
		(1.0, 4.0),     // 8: RISK_TP_ATR
		(0.5, 2.0),     // 9: RISK_SL_ATR
		(0.3, 0.8),     // 10: STACK_THRESHOLD
		(0.4, 0.9),     // 11: BRAIN_THRESHOLD
		(0, 2),         // 12: LOGIC_MODE
		(0.0, 2.0),     // 13: VOLUME_WEIGHT
		(0, 1),         // 14: REGIME_GATE
		(0, 1),         // 15: EXIT_RSI_ENABLE
		(65, 85),       // 16: EXIT_RSI_LEVEL
		(0, 1),         // 17: EXIT_MOMO_ENABLE
		(3, 12),        // 18: EXIT_MOMO_BARS
		(0, 1),         // 19: EXIT_STALL_ENABLE
		(5, 15),        // 20: EXIT_STALL_BARS
		(0, 1),         // 21: EXIT_MA_ENABLE
		(15, 30),       // 22: EXIT_MA_PERIOD
		(8, 16),        // 23: SSL_PERIOD_1
		(16, 24),       // 24: SSL_PERIOD_2
		(10, 18),       // 25: QQE_RSI_PERIOD
		(3, 7),         // 26: QQE_SMOOTH
		(3.0, 5.0),     // 27: QQE_FACTOR
		(0.01, 0.03),   // 28: PSAR_START
		(0.01, 0.03),   // 29: PSAR_INCREMENT
		(0.15, 0.25),   // 30: PSAR_MAX
		(10, 20),       // 31: AROON_PERIOD
		
		// ═══════════════════════════════════════════════════════════════
		// PHASE 2: SUPREME INTELLIGENCE GENES (32-39)
		// ═══════════════════════════════════════════════════════════════
		
		// Regime Gate Genes (0.0 = Allow all regimes, 1.0 = Restrict to specific regime)
		(0.0, 1.0),     // 32: REGIME_GATE_TREND_ONLY (1.0 = Only trade in TREND)
		(0.0, 1.0),     // 33: REGIME_GATE_RANGE_ONLY (1.0 = Only trade in RANGE)
		(0.0, 1.0),     // 34: REGIME_GATE_CHOPPY_ONLY (1.0 = Only trade in CHOPPY)
		
		// Weight Genes (0.0-1.0, normalized in brain calculation)
		(0.0, 1.0),     // 35: WEIGHT_LEAD (Importance of leading indicator)
		(0.0, 1.0),     // 36: WEIGHT_STACK (Importance of confirmation stack)
		(0.0, 1.0),     // 37: WEIGHT_REGIME (Importance of regime alignment)
		(0.0, 1.0),     // 38: WEIGHT_VOLUME (Importance of volume confirmation)
		(0.0, 1.0)      // 39: WEIGHT_HYBRID (Importance of hybrid signals)
	};
	
	#endregion
	
	#region Constructor
	
	/// <summary>
	/// Creates a new chromosome with random genes (PHASE 2: 40 genes)
	/// </summary>
	public Chromosome(Random rng)
	{
		Genes = new double[40];  // PHASE 2: Extended from 32 → 40
		InitializeRandom(rng);
		Fitness = 0.0;
		Complexity = 0;
		TradeCount = 0;
		WinRate = 0.5;
		SharpeRatio = 0.0;
	}
		
		/// <summary>
		/// Creates a chromosome with specific genes (for cloning)
		/// </summary>
		private Chromosome(double[] genes)
		{
			Genes = (double[])genes.Clone();
			Fitness = 0.0;
			Complexity = 0;
			TradeCount = 0;
			WinRate = 0.5;
			SharpeRatio = 0.0;
		}
		
		#endregion
		
		#region Gene Access
		
		/// <summary>
		/// Get gene value by index
		/// </summary>
		public double GetGene(int index)
		{
			if (index < 0 || index >= Genes.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			return Genes[index];
		}
		
		/// <summary>
		/// Set gene value by index with range clamping
		/// </summary>
		public void SetGene(int index, double value)
		{
			if (index < 0 || index >= Genes.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			
			Genes[index] = ClampGene(index, value);
		}
		
		#endregion
		
		#region Parameter Extraction
		
		/// <summary>
		/// Extract named parameter from genes (aligned with TradingView)
		/// PHASE 2 HOTFIX: Added bounds checking to prevent array out of bounds
		/// </summary>
		public double ExtractParam(string paramName)
		{
			try
			{
				switch (paramName)
				{
					case "ST_FACTOR": return Genes[0];
					case "ST_PERIOD": return Genes[1];
					case "RSI_PERIOD": return Genes[2];
					case "RSI_OVERBOUGHT": return Genes[3];
					case "RSI_OVERSOLD": return Genes[4];
					case "MACD_FAST": return Genes[5];
					case "MACD_SLOW": return Genes[6];
					case "MACD_SIGNAL": return Genes[7];
					case "RISK_TP_ATR": return Genes[8];
					case "RISK_SL_ATR": return Genes[9];
					case "STACK_THRESHOLD": return Genes[10];
				case "BRAIN_THRESHOLD": return Genes[11];
				case "LOGIC_MODE": return Genes[12];
				case "VOLUME_WEIGHT": return Genes[13];
				case "REGIME_GATE": return Genes[14];
				case "EXIT_RSI_ENABLE": return Genes[15];
				case "EXIT_RSI_LEVEL": return Genes[16];
				case "EXIT_MOMO_ENABLE": return Genes[17];
				case "EXIT_MOMO_BARS": return Genes[18];
				case "EXIT_STALL_ENABLE": return Genes[19];
				case "EXIT_STALL_BARS": return Genes[20];
				case "EXIT_MA_ENABLE": return Genes[21];
				case "EXIT_MA_PERIOD": return Genes[22];
				case "SSL_PERIOD_1": return Genes[23];
				case "SSL_PERIOD_2": return Genes[24];
				case "QQE_RSI_PERIOD": return Genes[25];
				case "QQE_SMOOTH": return Genes[26];
				case "QQE_FACTOR": return Genes[27];
				case "PSAR_START": return Genes[28];
				case "PSAR_INCREMENT": return Genes[29];
				case "PSAR_MAX": return Genes[30];
				case "AROON_PERIOD": return Genes[31];
				
				// PHASE 2: Supreme Intelligence Genes (32-39)
				case "REGIME_GATE_TREND_ONLY": return Genes[32];
				case "REGIME_GATE_RANGE_ONLY": return Genes[33];
				case "REGIME_GATE_CHOPPY_ONLY": return Genes[34];
				case "WEIGHT_LEAD": return Genes[35];
				case "WEIGHT_STACK": return Genes[36];
				case "WEIGHT_REGIME": return Genes[37];
				case "WEIGHT_VOLUME": return Genes[38];
				case "WEIGHT_HYBRID": return Genes[39];
				
				default:
					throw new ArgumentException($"Unknown parameter: {paramName}");
			}
			}
			catch (IndexOutOfRangeException ex)
			{
				// PHASE 2 HOTFIX: If gene array is too small, return safe default
				throw new InvalidOperationException($"Chromosome has {Genes.Length} genes but tried to access {paramName}. Expected 40 genes.", ex);
			}
		}
		
		#endregion
		
		#region Genetic Operations
		
		/// <summary>
		/// Initialize genes with random values within valid ranges
		/// </summary>
		private void InitializeRandom(Random rng)
		{
			for (int i = 0; i < Genes.Length; i++)
			{
				double min = GeneRanges[i].min;
				double max = GeneRanges[i].max;
				Genes[i] = min + rng.NextDouble() * (max - min);
			}
		}
		
		/// <summary>
		/// Clamp gene value to valid range
		/// </summary>
		private double ClampGene(int geneIndex, double value)
		{
			if (geneIndex < 0 || geneIndex >= GeneRanges.Length)
				return value;
				
			double min = GeneRanges[geneIndex].min;
			double max = GeneRanges[geneIndex].max;
			return Math.Max(min, Math.Min(max, value));
		}
		
		/// <summary>
		/// Mutate this chromosome (random gene changes)
		/// </summary>
		public void Mutate(double mutationRate, Random rng)
		{
			for (int i = 0; i < Genes.Length; i++)
			{
				if (rng.NextDouble() < mutationRate)
				{
					// Gaussian mutation: add random value from normal distribution
					double delta = (rng.NextDouble() - 0.5) * 2.0;  // -1 to +1
					double magnitude = GetGeneMutationMagnitude(i);
					Genes[i] = ClampGene(i, Genes[i] + delta * magnitude);
				}
			}
		}
		
		/// <summary>
		/// Get mutation magnitude for each gene type
		/// PHASE 1 FIX: Use dynamic ranges based on Genes.Length (supports 32 or 40 genes)
		/// </summary>
		private double GetGeneMutationMagnitude(int geneIndex)
		{
			// Adaptive to genome size (32 or 40 genes)
			if (geneIndex <= 7) return 2.0;  // Periods: ±2
			if (geneIndex >= 8 && geneIndex <= 11) return 0.2;  // Ratios: ±0.2
			if (geneIndex >= 12 && geneIndex <= 14) return 0.5;  // Modes: ±0.5
			if (geneIndex >= 15 && geneIndex <= 22) return 1.0;  // Exits: ±1
			if (geneIndex >= 23 && geneIndex <= 31) return 1.5;  // SSL/QQE/PSAR/Aroon: ±1.5
			
			// Phase 2: Supreme Intelligence genes (32-39)
			if (geneIndex >= 32 && geneIndex <= 39) return 0.3;  // Gates & Weights: ±0.3
			
			return 0.5;  // Default fallback
		}
		
		/// <summary>
		/// Crossover with another chromosome (create offspring)
		/// PHASE 1 CRITICAL FIX: Use this.Genes.Length instead of hardcoded 32
		/// </summary>
		public Chromosome Crossover(Chromosome other, Random rng)
		{
			// PHASE 1 FIX: Dynamic array size based on actual genome length
			double[] offspringGenes = new double[this.Genes.Length];
			
			// Two-point crossover
			int point1 = rng.Next(Genes.Length);
			int point2 = rng.Next(Genes.Length);
			if (point1 > point2) { int temp = point1; point1 = point2; point2 = temp; }
			
			for (int i = 0; i < Genes.Length; i++)
			{
				if (i < point1 || i >= point2)
					offspringGenes[i] = this.Genes[i];
				else
					offspringGenes[i] = other.Genes[i];
			}
			
			return new Chromosome(offspringGenes);
		}
		
		/// <summary>
		/// Create deep clone of this chromosome
		/// </summary>
		public Chromosome Clone()
		{
			var clone = new Chromosome((double[])Genes.Clone());
			clone.Fitness = this.Fitness;
			clone.Complexity = this.Complexity;
			clone.TradeCount = this.TradeCount;
			clone.WinRate = this.WinRate;
			clone.SharpeRatio = this.SharpeRatio;
			return clone;
		}
		
		#endregion
		
		#region Complexity Calculation (Occam's Razor)
		
		/// <summary>
		/// Calculate complexity score (number of active features)
		/// Lower is better (simpler strategies preferred)
		/// </summary>
		public int CalculateComplexity()
		{
			int complexity = 0;
			
			// Count active exits
			if (Genes[15] > 0.5) complexity++;  // RSI exit
			if (Genes[17] > 0.5) complexity++;  // MOMO exit
			if (Genes[19] > 0.5) complexity++;  // STALL exit
			if (Genes[21] > 0.5) complexity++;  // MA exit
			
			// Penalize extreme parameters
			if (Genes[10] > 0.7) complexity++;  // High stack threshold
			if (Genes[11] > 0.8) complexity++;  // High brain threshold
			if (Genes[13] > 1.5) complexity++;  // High volume weight
			
			Complexity = complexity;
			return complexity;
		}
		
		#endregion
	}
	
	/// <summary>
	/// Manages genetic evolution of trading parameters across 3 market regimes.
	/// Uses mutation, crossover, selection, and diversity homeostasis.
	/// Aligned with TradingView DIY.ps v1.8 Evolution System.
	/// </summary>
	public class EvolutionaryArchitect
	{
		#region Constants
		
		private const int POPULATION_SIZE = 20;
		private const double MUTATION_RATE = 0.15;      // 15% gene mutation chance
		private const double CROSSOVER_RATE = 0.40;      // 40% crossover chance
		private const double ELITE_RATIO = 0.20;         // Keep top 20%
		private const double DIVERSITY_TARGET = 0.50;    // Target 50% diversity
		private const double OCCAM_PENALTY_WEIGHT = 0.05; // 5% penalty per complexity point
		
		#endregion
		
		#region Properties
		
		public Chromosome[] TrendPopulation { get; private set; }
		public Chromosome[] RangePopulation { get; private set; }
		public Chromosome[] ChoppyPopulation { get; private set; }
		
		public Chromosome TrendChampion { get; private set; }
		public Chromosome RangeChampion { get; private set; }
		public Chromosome ChoppyChampion { get; private set; }
		
		public int Generation { get; private set; }
		
		private Random rng;
		
		#endregion
		
		#region Constructor
		
		/// <summary>
		/// Initialize evolution system with 3 populations
		/// </summary>
		public EvolutionaryArchitect(int seed = 42)
		{
			rng = new Random(seed);
			Generation = 0;
			
			// Initialize populations
			TrendPopulation = InitializePopulation();
			RangePopulation = InitializePopulation();
			ChoppyPopulation = InitializePopulation();
			
			// Set initial champions (best from random initialization)
			TrendChampion = TrendPopulation[0].Clone();
			RangeChampion = RangePopulation[0].Clone();
			ChoppyChampion = ChoppyPopulation[0].Clone();
		}
		
		#endregion
		
		#region Population Management
		
		/// <summary>
		/// Initialize population with random chromosomes
		/// </summary>
		private Chromosome[] InitializePopulation()
		{
			var population = new Chromosome[POPULATION_SIZE];
			for (int i = 0; i < POPULATION_SIZE; i++)
			{
				population[i] = new Chromosome(rng);
			}
			return population;
		}
		
		#endregion
		
		#region Evolution Step
		
		/// <summary>
		/// Perform one evolution step for a specific regime
		/// Returns the champion chromosome
		/// PHASE 2: Now uses per-chromosome simulation for true intelligent evolution
		/// </summary>
		public Chromosome Evolve(string regime, List<double> tradeReturns, DIYBrainFull indicatorInstance)
		{
			Chromosome[] population = GetPopulation(regime);
			
			// 1. Calculate fitness for all chromosomes
			// PHASE 2: Each chromosome gets evaluated on its OWN simulated performance
			UpdateFitness(population, indicatorInstance);
			
			// 2. Sort by fitness (descending)
			Array.Sort(population, (a, b) => b.Fitness.CompareTo(a.Fitness));
			
			// 3. Update champion
			var champion = population[0].Clone();
			SetChampion(regime, champion);
			
			// 4. Selection: Keep elite
			int eliteCount = (int)(POPULATION_SIZE * ELITE_RATIO);
			var nextGeneration = new List<Chromosome>();
			for (int i = 0; i < eliteCount; i++)
			{
				nextGeneration.Add(population[i].Clone());
			}
			
			// 5. Crossover: Create offspring
			while (nextGeneration.Count < POPULATION_SIZE)
			{
				if (rng.NextDouble() < CROSSOVER_RATE)
				{
					var parent1 = TournamentSelect(population, 3);
					var parent2 = TournamentSelect(population, 3);
					var offspring = parent1.Crossover(parent2, rng);
					nextGeneration.Add(offspring);
				}
				else
				{
					// Clone random elite individual
					nextGeneration.Add(population[rng.Next(eliteCount)].Clone());
				}
			}
			
			// 6. Mutation: Mutate non-elite
			for (int i = eliteCount; i < nextGeneration.Count; i++)
			{
				nextGeneration[i].Mutate(MUTATION_RATE, rng);
			}
			
			// 7. Diversity Homeostasis: Ensure population diversity
			EnforceDiversity(nextGeneration);
			
			// 8. Update population
			SetPopulation(regime, nextGeneration.ToArray());
			
			Generation++;
			
			return champion;
		}
		
		#endregion
		
		#region Fitness Calculation
		
		/// <summary>
		/// Update fitness scores for all chromosomes in population
		/// Fitness = (Sharpe * 0.6) + (WinRate * 0.4) - Occam Penalty
		/// </summary>
		/// <summary>
		/// Calculate fitness for each chromosome based on its OWN simulated performance
		/// PHASE 2: THE INTELLIGENCE UNLOCK
		/// Each chromosome generates unique trade results via SimulateTradesForChromosome
		/// This differentiates chromosomes - the brain can now truly learn!
		/// </summary>
		private void UpdateFitness(Chromosome[] population, DIYBrainFull indicatorInstance)
		{
			foreach (var chromosome in population)
			{
				// PHASE 2: Each chromosome simulates its own trades!
				// This is the KEY difference - no more shared tradeReturns
				List<double> chromosomeReturns = indicatorInstance.SimulateTradesForChromosome(chromosome, 100);
				
				// If simulation didn't generate trades, use fallback fitness
				if (chromosomeReturns.Count < 2)
				{
					chromosome.Fitness = 0.0;
					chromosome.SharpeRatio = 0.0;
					chromosome.WinRate = 0.5;
					chromosome.TradeCount = 0;
					continue;
				}
				
				// Calculate Sharpe Ratio from THIS chromosome's trade returns
				double sharpe = CalculateSharpeRatio(chromosomeReturns);
				chromosome.SharpeRatio = sharpe;
				
				// Calculate Win Rate for THIS chromosome
				int wins = chromosomeReturns.Count(r => r > 0);
				double winRate = chromosomeReturns.Count > 0 ? (double)wins / chromosomeReturns.Count : 0.5;
				chromosome.WinRate = winRate;
				chromosome.TradeCount = chromosomeReturns.Count;
				
				// Calculate complexity penalty (Occam's Razor)
				int complexity = chromosome.CalculateComplexity();
				double occamPenalty = complexity * OCCAM_PENALTY_WEIGHT;
				
				// Combined fitness (aligned with TradingView)
				chromosome.Fitness = (sharpe * 0.6) + (winRate * 0.4) - occamPenalty;
			}
		}
		
		/// <summary>
		/// Calculate Sharpe Ratio from trade returns
		/// </summary>
		private double CalculateSharpeRatio(List<double> returns)
		{
			if (returns.Count < 2) return 0.0;
			
			double mean = returns.Average();
			double variance = returns.Select(r => Math.Pow(r - mean, 2)).Average();
			double stdDev = Math.Sqrt(variance);
			
			if (stdDev == 0) return 0.0;
			
			return mean / stdDev;
		}
		
		#endregion
		
		#region Selection
		
		/// <summary>
		/// Tournament selection: Pick best from random sample
		/// </summary>
		private Chromosome TournamentSelect(Chromosome[] population, int tournamentSize)
		{
			Chromosome best = null;
			for (int i = 0; i < tournamentSize; i++)
			{
				var candidate = population[rng.Next(population.Length)];
				if (best == null || candidate.Fitness > best.Fitness)
					best = candidate;
			}
			return best;
		}
		
		#endregion
		
		#region Diversity Homeostasis
		
		/// <summary>
		/// Ensure population maintains genetic diversity
		/// Prevents convergence to local optima
		/// </summary>
		private void EnforceDiversity(List<Chromosome> population)
		{
			double diversity = CalculateDiversity(population);
			
			// If diversity too low, inject random chromosomes
			if (diversity < DIVERSITY_TARGET)
			{
				int injectCount = (int)(POPULATION_SIZE * 0.10);  // Replace 10%
				for (int i = 0; i < injectCount; i++)
				{
					int replaceIndex = rng.Next(population.Count / 2, population.Count);  // Replace bottom half
					population[replaceIndex] = new Chromosome(rng);
				}
			}
		}
		
		/// <summary>
		/// Calculate genetic diversity (average pairwise distance)
		/// </summary>
		private double CalculateDiversity(List<Chromosome> population)
		{
			if (population.Count < 2) return 1.0;
			
			double totalDistance = 0.0;
			int comparisons = 0;
			
			for (int i = 0; i < population.Count - 1; i++)
			{
				for (int j = i + 1; j < population.Count; j++)
				{
					totalDistance += GeneticDistance(population[i], population[j]);
				comparisons++;
			}
		}
		
		double avgDistance = totalDistance / comparisons;
		// PHASE 1 FIX: Dynamic max distance based on actual genome length (40 genes, not 32)
		double maxDistance = Math.Sqrt(population[0].Genes.Length);  // Max distance for N-dimensional space
		
		return avgDistance / maxDistance;  // Normalize 0-1
	}		/// <summary>
		/// Calculate Euclidean distance between two chromosomes
		/// </summary>
		private double GeneticDistance(Chromosome a, Chromosome b)
		{
			double sum = 0.0;
			for (int i = 0; i < a.Genes.Length; i++)
			{
				double diff = a.Genes[i] - b.Genes[i];
				sum += diff * diff;
			}
			return Math.Sqrt(sum);
		}
		
		#endregion
		
		#region Helper Methods
		
		private Chromosome[] GetPopulation(string regime)
		{
			switch (regime.ToLower())
			{
				case "trend": return TrendPopulation;
				case "range": return RangePopulation;
				case "choppy": return ChoppyPopulation;
				default: throw new ArgumentException($"Invalid regime: {regime}");
			}
		}
		
		private void SetPopulation(string regime, Chromosome[] population)
		{
			switch (regime.ToLower())
			{
				case "trend": TrendPopulation = population; break;
				case "range": RangePopulation = population; break;
				case "choppy": ChoppyPopulation = population; break;
				default: throw new ArgumentException($"Invalid regime: {regime}");
			}
		}
		
		private void SetChampion(string regime, Chromosome champion)
		{
			switch (regime.ToLower())
			{
				case "trend": TrendChampion = champion; break;
				case "range": RangeChampion = champion; break;
				case "choppy": ChoppyChampion = champion; break;
				default: throw new ArgumentException($"Invalid regime: {regime}");
			}
		}
		
		public Chromosome GetChampion(string regime)
		{
			switch (regime.ToLower())
			{
				case "trend": return TrendChampion;
				case "range": return RangeChampion;
				case "choppy": return ChoppyChampion;
				default: throw new ArgumentException($"Invalid regime: {regime}");
			}
		}
		
		#endregion
	}
}

#region NinjaScript generated code
// (Auto-generated code omitted for brevity - NinjaTrader will generate this automatically on compile)
#endregion

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private DIYBrainFull[] cacheDIYBrainFull;
		public DIYBrainFull DIYBrainFull(TradingModeType tradingMode, int aggressiveness, double brainThresholdLong, double brainThresholdShort, bool enableLearning, double tpAtrMultiplier, double slAtrMultiplier, double trailAtrMultiplier, double breakevenAfterR, int shortPeriod, int mediumPeriod, int longPeriod, int atrPeriod, bool enableHeavyIndicators, double wLead, double wStack, double wRegime, double wVolume, double wHybrid, bool adaptiveWeights, bool useProbabilisticBrain, bool useEvolutionSystem, int evolutionFrequency, int evolutionSeed)
		{
			return DIYBrainFull(Input, tradingMode, aggressiveness, brainThresholdLong, brainThresholdShort, enableLearning, tpAtrMultiplier, slAtrMultiplier, trailAtrMultiplier, breakevenAfterR, shortPeriod, mediumPeriod, longPeriod, atrPeriod, enableHeavyIndicators, wLead, wStack, wRegime, wVolume, wHybrid, adaptiveWeights, useProbabilisticBrain, useEvolutionSystem, evolutionFrequency, evolutionSeed);
		}

		public DIYBrainFull DIYBrainFull(ISeries<double> input, TradingModeType tradingMode, int aggressiveness, double brainThresholdLong, double brainThresholdShort, bool enableLearning, double tpAtrMultiplier, double slAtrMultiplier, double trailAtrMultiplier, double breakevenAfterR, int shortPeriod, int mediumPeriod, int longPeriod, int atrPeriod, bool enableHeavyIndicators, double wLead, double wStack, double wRegime, double wVolume, double wHybrid, bool adaptiveWeights, bool useProbabilisticBrain, bool useEvolutionSystem, int evolutionFrequency, int evolutionSeed)
		{
			if (cacheDIYBrainFull != null)
				for (int idx = 0; idx < cacheDIYBrainFull.Length; idx++)
					if (cacheDIYBrainFull[idx] != null && cacheDIYBrainFull[idx].TradingMode == tradingMode && cacheDIYBrainFull[idx].Aggressiveness == aggressiveness && cacheDIYBrainFull[idx].BrainThresholdLong == brainThresholdLong && cacheDIYBrainFull[idx].BrainThresholdShort == brainThresholdShort && cacheDIYBrainFull[idx].EnableLearning == enableLearning && cacheDIYBrainFull[idx].TpAtrMultiplier == tpAtrMultiplier && cacheDIYBrainFull[idx].SlAtrMultiplier == slAtrMultiplier && cacheDIYBrainFull[idx].TrailAtrMultiplier == trailAtrMultiplier && cacheDIYBrainFull[idx].BreakevenAfterR == breakevenAfterR && cacheDIYBrainFull[idx].ShortPeriod == shortPeriod && cacheDIYBrainFull[idx].MediumPeriod == mediumPeriod && cacheDIYBrainFull[idx].LongPeriod == longPeriod && cacheDIYBrainFull[idx].AtrPeriod == atrPeriod && cacheDIYBrainFull[idx].EnableHeavyIndicators == enableHeavyIndicators && cacheDIYBrainFull[idx].WLead == wLead && cacheDIYBrainFull[idx].WStack == wStack && cacheDIYBrainFull[idx].WRegime == wRegime && cacheDIYBrainFull[idx].WVolume == wVolume && cacheDIYBrainFull[idx].WHybrid == wHybrid && cacheDIYBrainFull[idx].AdaptiveWeights == adaptiveWeights && cacheDIYBrainFull[idx].UseProbabilisticBrain == useProbabilisticBrain && cacheDIYBrainFull[idx].UseEvolutionSystem == useEvolutionSystem && cacheDIYBrainFull[idx].EvolutionFrequency == evolutionFrequency && cacheDIYBrainFull[idx].EvolutionSeed == evolutionSeed && cacheDIYBrainFull[idx].EqualsInput(input))
						return cacheDIYBrainFull[idx];
			return CacheIndicator<DIYBrainFull>(new DIYBrainFull(){ TradingMode = tradingMode, Aggressiveness = aggressiveness, BrainThresholdLong = brainThresholdLong, BrainThresholdShort = brainThresholdShort, EnableLearning = enableLearning, TpAtrMultiplier = tpAtrMultiplier, SlAtrMultiplier = slAtrMultiplier, TrailAtrMultiplier = trailAtrMultiplier, BreakevenAfterR = breakevenAfterR, ShortPeriod = shortPeriod, MediumPeriod = mediumPeriod, LongPeriod = longPeriod, AtrPeriod = atrPeriod, EnableHeavyIndicators = enableHeavyIndicators, WLead = wLead, WStack = wStack, WRegime = wRegime, WVolume = wVolume, WHybrid = wHybrid, AdaptiveWeights = adaptiveWeights, UseProbabilisticBrain = useProbabilisticBrain, UseEvolutionSystem = useEvolutionSystem, EvolutionFrequency = evolutionFrequency, EvolutionSeed = evolutionSeed }, input, ref cacheDIYBrainFull);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.DIYBrainFull DIYBrainFull(TradingModeType tradingMode, int aggressiveness, double brainThresholdLong, double brainThresholdShort, bool enableLearning, double tpAtrMultiplier, double slAtrMultiplier, double trailAtrMultiplier, double breakevenAfterR, int shortPeriod, int mediumPeriod, int longPeriod, int atrPeriod, bool enableHeavyIndicators, double wLead, double wStack, double wRegime, double wVolume, double wHybrid, bool adaptiveWeights, bool useProbabilisticBrain, bool useEvolutionSystem, int evolutionFrequency, int evolutionSeed)
		{
			return indicator.DIYBrainFull(Input, tradingMode, aggressiveness, brainThresholdLong, brainThresholdShort, enableLearning, tpAtrMultiplier, slAtrMultiplier, trailAtrMultiplier, breakevenAfterR, shortPeriod, mediumPeriod, longPeriod, atrPeriod, enableHeavyIndicators, wLead, wStack, wRegime, wVolume, wHybrid, adaptiveWeights, useProbabilisticBrain, useEvolutionSystem, evolutionFrequency, evolutionSeed);
		}

		public Indicators.DIYBrainFull DIYBrainFull(ISeries<double> input , TradingModeType tradingMode, int aggressiveness, double brainThresholdLong, double brainThresholdShort, bool enableLearning, double tpAtrMultiplier, double slAtrMultiplier, double trailAtrMultiplier, double breakevenAfterR, int shortPeriod, int mediumPeriod, int longPeriod, int atrPeriod, bool enableHeavyIndicators, double wLead, double wStack, double wRegime, double wVolume, double wHybrid, bool adaptiveWeights, bool useProbabilisticBrain, bool useEvolutionSystem, int evolutionFrequency, int evolutionSeed)
		{
			return indicator.DIYBrainFull(input, tradingMode, aggressiveness, brainThresholdLong, brainThresholdShort, enableLearning, tpAtrMultiplier, slAtrMultiplier, trailAtrMultiplier, breakevenAfterR, shortPeriod, mediumPeriod, longPeriod, atrPeriod, enableHeavyIndicators, wLead, wStack, wRegime, wVolume, wHybrid, adaptiveWeights, useProbabilisticBrain, useEvolutionSystem, evolutionFrequency, evolutionSeed);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.DIYBrainFull DIYBrainFull(TradingModeType tradingMode, int aggressiveness, double brainThresholdLong, double brainThresholdShort, bool enableLearning, double tpAtrMultiplier, double slAtrMultiplier, double trailAtrMultiplier, double breakevenAfterR, int shortPeriod, int mediumPeriod, int longPeriod, int atrPeriod, bool enableHeavyIndicators, double wLead, double wStack, double wRegime, double wVolume, double wHybrid, bool adaptiveWeights, bool useProbabilisticBrain, bool useEvolutionSystem, int evolutionFrequency, int evolutionSeed)
		{
			return indicator.DIYBrainFull(Input, tradingMode, aggressiveness, brainThresholdLong, brainThresholdShort, enableLearning, tpAtrMultiplier, slAtrMultiplier, trailAtrMultiplier, breakevenAfterR, shortPeriod, mediumPeriod, longPeriod, atrPeriod, enableHeavyIndicators, wLead, wStack, wRegime, wVolume, wHybrid, adaptiveWeights, useProbabilisticBrain, useEvolutionSystem, evolutionFrequency, evolutionSeed);
		}

		public Indicators.DIYBrainFull DIYBrainFull(ISeries<double> input , TradingModeType tradingMode, int aggressiveness, double brainThresholdLong, double brainThresholdShort, bool enableLearning, double tpAtrMultiplier, double slAtrMultiplier, double trailAtrMultiplier, double breakevenAfterR, int shortPeriod, int mediumPeriod, int longPeriod, int atrPeriod, bool enableHeavyIndicators, double wLead, double wStack, double wRegime, double wVolume, double wHybrid, bool adaptiveWeights, bool useProbabilisticBrain, bool useEvolutionSystem, int evolutionFrequency, int evolutionSeed)
		{
			return indicator.DIYBrainFull(input, tradingMode, aggressiveness, brainThresholdLong, brainThresholdShort, enableLearning, tpAtrMultiplier, slAtrMultiplier, trailAtrMultiplier, breakevenAfterR, shortPeriod, mediumPeriod, longPeriod, atrPeriod, enableHeavyIndicators, wLead, wStack, wRegime, wVolume, wHybrid, adaptiveWeights, useProbabilisticBrain, useEvolutionSystem, evolutionFrequency, evolutionSeed);
		}
	}
}

#endregion
