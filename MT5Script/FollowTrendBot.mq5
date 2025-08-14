//+------------------------------------------------------------------+
//|                                             FollowTrendBot.mq5 |
//|              MT5 EA that trades based on trend and entry signals |
//+------------------------------------------------------------------+
#property copyright "FollowTrendBot"
#property version   "1.00"
#property strict

// Include necessary libraries
#include <Trade/Trade.mqh>        // For trade operations
#include <Arrays/ArrayObj.mqh>    // For dynamic arrays

// Input parameters
input string   API_Key = "your_api_key_here";               // API Key for authentication
input string   API_Endpoint_Trend = "https://tradingsignals-ae14b4a15912.herokuapp.com/api/trend";  // URL for trend signals
input string   API_Endpoint_Entry = "https://tradingsignals-ae14b4a15912.herokuapp.com/api/entry";  // URL for entry signals
input string   PUT_Endpoint_Base = "https://tradingsignals-ae14b4a15912.herokuapp.com/api/ActiveSignals/markused/";  // Base URL for PUT updates
input double   SL_Pips = 50.0;             // Stop Loss in pips
input double   TP_Pips = 100.0;            // Take Profit in pips
input double   Y_Pips_Breakeven = 50.0;    // Pips needed for partial close + breakeven
input double   Lot_Size = 0.01;            // Trading lot size
input int      Magic_Number = 12345;       // Magic number for orders
input int      Max_Slippage = 3;           // Maximum allowed slippage in pips
input bool     Enable_Logging = true;      // Enable detailed logging

// Signal structure
struct SignalInfo {
   int      id;           // Signal ID
   string   action;       // "Buy" or "Sell"
   double   price;        // Signal price
   bool     used;         // If signal has been used
   datetime timestamp;    // When signal was received
};

// Global variables
CTrade         g_trade;            // Trade object
SignalInfo     g_trendSignal;      // Current trend signal
SignalInfo     g_entrySignal;      // Current entry signal
int            g_lastTrendId = 0;  // Last processed trend ID
int            g_lastEntryId = 0;  // Last processed entry ID
int            g_apiErrors = 0;    // Counter for API errors
bool           g_partialClosed = false; // Flag for partial close
datetime       g_lastTrendCheck = 0;    // Last time trend was checked
datetime       g_lastEntryCheck = 0;    // Last time entry was checked

// UI labels
string         LABEL_TREND = "FTB_TrendLabel";
string         LABEL_ENTRY = "FTB_EntryLabel";

// Forward declarations
void CreateOrUpdateLabels();
void UpdateLabelsText();

//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit() {
   // Initialize trade object
   g_trade.SetExpertMagicNumber(Magic_Number);
   g_trade.SetMarginMode();
   g_trade.SetTypeFillingBySymbol(_Symbol);
   g_trade.SetDeviationInPoints(Max_Slippage);
   
   // Initialize signals
   g_trendSignal.id = 0;
   g_trendSignal.action = "";
   g_trendSignal.price = 0;
   g_trendSignal.used = true;
   g_trendSignal.timestamp = 0;
   
   g_entrySignal.id = 0;
   g_entrySignal.action = "";
   g_entrySignal.price = 0;
   g_entrySignal.used = true;
   g_entrySignal.timestamp = 0;
   
   // Set timer for API checks (every 10 seconds for more responsive updates)
   EventSetTimer(10);
   
   // Create UI labels and show initial values
   CreateOrUpdateLabels();
   UpdateLabelsText();

   // Fetch signals immediately on attach to populate labels
   if(Enable_Logging) Print("Initial fetch on attach: TREND & ENTRY");
   GetTrendSignal();
   GetEntrySignal();
   UpdateLabelsText();
   // Optionally attempt processing immediately
   ProcessSignals();

   // Log initialization
   if(Enable_Logging) {
      Print("FollowTrendBot initialized");
      Print("Symbol: ", _Symbol, ", Timeframe: ", EnumToString(Period()));
      Print("Trend API: ", API_Endpoint_Trend);
      Print("Entry API: ", API_Endpoint_Entry);
   }
   
   return(INIT_SUCCEEDED);
}

//+------------------------------------------------------------------+
//| Expert deinitialization function                                 |
//+------------------------------------------------------------------+
void OnDeinit(const int reason) {
   // Remove timer
   EventKillTimer();
   
   // Remove labels
   ObjectDelete(0, LABEL_TREND);
   ObjectDelete(0, LABEL_ENTRY);
   
   if(Enable_Logging)
      Print("FollowTrendBot stopped, reason: ", reason);
}

//+------------------------------------------------------------------+
//| Timer function for periodic API checks                           |
//+------------------------------------------------------------------+
void OnTimer() {
   // Get current time
   MqlDateTime dt;
   TimeCurrent(dt);
   datetime currentTime = TimeCurrent();
   
   if(Enable_Logging) {
      Print("\n----- OnTimer fired at ", TimeToString(currentTime), ", min=", dt.min, ", sec=", dt.sec, 
            " -----");
      Print("Last trend check: ", TimeToString(g_lastTrendCheck), 
            ", Last entry check: ", TimeToString(g_lastEntryCheck));
   }
   
   // Check trend signal at minutes 0 and 30 of each hour or if never checked
   bool shouldCheckTrend = g_lastTrendCheck == 0 || // never checked
                          (dt.min == 0 || dt.min == 30) || // on scheduled minute
                          (currentTime - g_lastTrendCheck) > 60*30; // or if >30min passed
   
   if(shouldCheckTrend) {
      if(currentTime - g_lastTrendCheck >= 30) { // Prevent checks too often (30s cooldown)
         g_lastTrendCheck = currentTime;
         if(Enable_Logging)
            Print("Checking trend signal at ", TimeToString(currentTime));
         GetTrendSignal();
      }
      else if(Enable_Logging) {
         Print("Trend check cooldown active, last check=", TimeToString(g_lastTrendCheck));
      }
   }
   else if(Enable_Logging) {
      Print("Not trend check time, next at minute 00 or 30");
   }
   
   // Check entry signal every minute or if never checked
   bool shouldCheckEntry = g_lastEntryCheck == 0 || // never checked
                          (currentTime - g_lastEntryCheck) >= 60; // at least 1min passed
   
   if(shouldCheckEntry) {
      g_lastEntryCheck = currentTime;
      if(Enable_Logging)
         Print("Checking entry signal at ", TimeToString(currentTime));
      GetEntrySignal();
         
      // Process signals after getting entry
      ProcessSignals();
   }
   else if(Enable_Logging) {
      Print("Entry check cooldown active, last check=", TimeToString(g_lastEntryCheck));
   }
   
   // Force chart to update UI
   ChartRedraw();
}

//+------------------------------------------------------------------+
//| Tick function for monitoring open positions                      |
//+------------------------------------------------------------------+
void OnTick() {
   // Check for partial close opportunity on open positions
   ManageOpenPositions();
   
   // Handle potential reversal on entry signal
   CheckForReversals();
}

//+------------------------------------------------------------------+
//| Get trend signal from API                                        |
//+------------------------------------------------------------------+
void GetTrendSignal() {
   string response = "";
   uchar result[];
   uchar data[]; // empty payload for GET
   string headers = "Authorization: Bearer " + API_Key + "\r\n";
   
   // Send API request
   string response_headers = "";
   if(Enable_Logging)
      Print("Requesting TREND: ", API_Endpoint_Trend);
   int res = WebRequest("GET", API_Endpoint_Trend, headers, 5000, data, result, response_headers);
   
   if(res == 200) { // Success
      response = CharArrayToString(result);
      if(Enable_Logging) {
         string preview = (StringLen(response) > 300 ? StringSubstr(response, 0, 300) + "..." : response);
         Print("Trend API status=200, resp_len=", StringLen(response), ", headers=", response_headers);
         Print("Trend API response preview: ", preview);
      }
      
      // Parse JSON response
      ParseSignalJSON(response, g_trendSignal);
      if(Enable_Logging)
         Print("Trend parsed -> id=", g_trendSignal.id, ", action=", g_trendSignal.action, 
               ", price=", DoubleToString(g_trendSignal.price, _Digits), ", used=", (g_trendSignal.used?"true":"false"));
      UpdateLabelsText();
      
      // Reset error counter on success
      g_apiErrors = 0;
   } else {
      // Handle API error
      g_apiErrors++;
      int le = GetLastError();
      Print("Trend API failed: status=", res, ", lastError=", le, ", headers=", response_headers);
      if(le == 4066 || le == 4016) {
         Print("Hint: Allow WebRequest URL in MT5 Options > Expert Advisors: ", API_Endpoint_Trend);
      }
      
      // Stop bot after multiple errors
      if(g_apiErrors >= 3) {
         Print("Multiple API errors detected, stopping bot");
         ExpertRemove();
      }
   }
}

//+------------------------------------------------------------------+
//| Get entry signal from API                                        |
//+------------------------------------------------------------------+
void GetEntrySignal() {
   string response = "";
   uchar result[];
   uchar data[]; // empty payload for GET
   string headers = "Authorization: Bearer " + API_Key + "\r\n";
   
   // Send API request
   string response_headers = "";
    if(Enable_Logging)
       Print("Requesting ENTRY: ", API_Endpoint_Entry);
   int res = WebRequest("GET", API_Endpoint_Entry, headers, 5000, data, result, response_headers);
   
   if(res == 200) { // Success
      response = CharArrayToString(result);
      if(Enable_Logging) {
         string preview = (StringLen(response) > 300 ? StringSubstr(response, 0, 300) + "..." : response);
         Print("Entry API status=200, resp_len=", StringLen(response), ", headers=", response_headers);
         Print("Entry API response preview: ", preview);
      }
      
      // Parse JSON response
      ParseSignalJSON(response, g_entrySignal);
      if(Enable_Logging)
         Print("Entry parsed -> id=", g_entrySignal.id, ", action=", g_entrySignal.action, 
               ", price=", DoubleToString(g_entrySignal.price, _Digits), ", used=", (g_entrySignal.used?"true":"false"));
      UpdateLabelsText();
      
      // Reset error counter on success
      g_apiErrors = 0;
   } else {
      // Handle API error
      g_apiErrors++;
      int le = GetLastError();
      Print("Entry API failed: status=", res, ", lastError=", le, ", headers=", response_headers);
      if(le == 4066 || le == 4016) {
         Print("Hint: Allow WebRequest URL in MT5 Options > Expert Advisors: ", API_Endpoint_Entry);
      }
      
      // Stop bot after multiple errors
      if(g_apiErrors >= 3) {
         Print("Multiple API errors detected, stopping bot");
         ExpertRemove();
      }
   }
}

//+------------------------------------------------------------------+
//| Parse JSON response from API                                     |
//+------------------------------------------------------------------+
void ParseSignalJSON(string json, SignalInfo &signal) {
   // Manual simple JSON parsing for the expected format:
   // {"id": int, "action": "Buy" or "Sell", "price": double, "used": bool}
   
   // Extract id
   int idPos = StringFind(json, "\"id\":");
   if(idPos >= 0) {
      int idStartPos = idPos + 5; // Skip "id":
      while(StringGetCharacter(json, idStartPos) == ' ') idStartPos++; // Skip spaces
      
      string idStr = "";
      for(int i = idStartPos; i < StringLen(json); i++) {
         ushort c = StringGetCharacter(json, i);
         if(c >= '0' && c <= '9')
            idStr += ShortToString(c);
         else
            break;
      }
      
      if(idStr != "")
         signal.id = (int)StringToInteger(idStr);
   }
   
   // Extract action
   int actionPos = StringFind(json, "\"action\":");
   if(actionPos >= 0) {
      int actionStartPos = StringFind(json, "\"", actionPos + 9) + 1;
      int actionEndPos = StringFind(json, "\"", actionStartPos);
      if(actionStartPos > 0 && actionEndPos > actionStartPos)
         signal.action = StringSubstr(json, actionStartPos, actionEndPos - actionStartPos);
   }
   
   // Extract price
   int pricePos = StringFind(json, "\"price\":");
   if(pricePos >= 0) {
      int priceStartPos = pricePos + 8; // Skip "price":
      while(StringGetCharacter(json, priceStartPos) == ' ') priceStartPos++; // Skip spaces
      
      string priceStr = "";
      for(int i = priceStartPos; i < StringLen(json); i++) {
         ushort c = StringGetCharacter(json, i);
         if((c >= '0' && c <= '9') || c == '.')
            priceStr += ShortToString(c);
         else
            break;
      }
      
      if(priceStr != "")
         signal.price = StringToDouble(priceStr);
   }
   
   // Extract used
   int usedPos = StringFind(json, "\"used\":");
   if(usedPos >= 0) {
      int usedStartPos = usedPos + 7; // Skip "used":
      while(StringGetCharacter(json, usedStartPos) == ' ') usedStartPos++; // Skip spaces
      
      string usedStr = "";
      for(int i = usedStartPos; i < StringLen(json); i++) {
         ushort c = StringGetCharacter(json, i);
         // Look for true or false
         if(c == 't' || c == 'f') {
            usedStr = StringSubstr(json, i, 4); // "true"
            if(StringCompare(usedStr, "true") == 0) {
               signal.used = true;
               break;
            }
            
            usedStr = StringSubstr(json, i, 5); // "false"
            if(StringCompare(usedStr, "false") == 0) {
               signal.used = false;
               break;
            }
         }
      }
   }
   
   // Update timestamp
   signal.timestamp = TimeCurrent();
   
   // Log parsed signal
   if(Enable_Logging) {
      Print("Parsed signal - ID: ", signal.id, 
            ", Action: ", signal.action, 
            ", Price: ", signal.price, 
            ", Used: ", signal.used);
   }
}

//+------------------------------------------------------------------+
//| Process trend and entry signals                                  |
//+------------------------------------------------------------------+
void ProcessSignals() {
   // Only process if we have both signals and neither is used
   if(g_trendSignal.action == "" || g_entrySignal.action == "") {
      if(Enable_Logging) {
         Print("Skipping trade: missing signal(s). trend_action=", g_trendSignal.action, 
               ", entry_action=", g_entrySignal.action);
      }
      return;
   }
   
   if(g_trendSignal.used || g_entrySignal.used) {
      if(Enable_Logging)
         Print("Skipping trade: used flags. trend_used=", (g_trendSignal.used?"true":"false"), 
               ", entry_used=", (g_entrySignal.used?"true":"false"));
      return;
   }
   
   // Check if signals match
   if(g_trendSignal.action == g_entrySignal.action) {
      // We have matching, unused signals - validate price
      double currentPrice = 0;
      if(g_entrySignal.action == "Buy")
         currentPrice = SymbolInfoDouble(_Symbol, SYMBOL_ASK);
      else if(g_entrySignal.action == "Sell")
         currentPrice = SymbolInfoDouble(_Symbol, SYMBOL_BID);
      
      // Calculate price difference in pips
      double point = SymbolInfoDouble(_Symbol, SYMBOL_POINT);
      double pips_per_point = 10; // For 5-digit brokers; use 1 for 4-digit
      double priceDiffInPips = MathAbs(currentPrice - g_entrySignal.price) / (point * pips_per_point);
      if(Enable_Logging)
         Print("Validation: current=", DoubleToString(currentPrice, _Digits), 
               ", signal=", DoubleToString(g_entrySignal.price, _Digits), 
               ", diff_pips=", DoubleToString(priceDiffInPips, 1));
      
      // Validate entry price not too far from current price
      if(priceDiffInPips <= 10) { // Max 10 pips difference
         // Check for existing positions in the opposite direction
         if(Enable_Logging)
            Print("Signals aligned (", g_entrySignal.action, ") and within threshold. Checking opposite positions...");
         CloseOppositePositions(g_entrySignal.action);
         
         // Execute trade
         ExecuteTrade();
         
         // Mark entry as used
         MarkEntryAsUsed();
      } else {
         if(Enable_Logging)
            Print("Price difference too large: ", priceDiffInPips, " pips. Signal price: ", 
                  g_entrySignal.price, ", Current price: ", currentPrice);
      }
   } else {
      if(Enable_Logging)
         Print("Skipping trade: signals mismatch. trend=", g_trendSignal.action, 
               ", entry=", g_entrySignal.action);
   }
}

//+------------------------------------------------------------------+
//| Close any positions in the opposite direction                    |
//+------------------------------------------------------------------+
void CloseOppositePositions(string action) {
   string oppositeAction = (action == "Buy") ? "Sell" : "Buy";
   
   // Check if we have any positions in the opposite direction
   bool anyClosed = false;
   for(int i = PositionsTotal() - 1; i >= 0; i--) {
      ulong posTicket = PositionGetTicket(i);
      if(posTicket <= 0) continue;
      
      // Only look at positions for our symbol and magic number
      if(!PositionSelectByTicket(posTicket)) continue;
      if(PositionGetString(POSITION_SYMBOL) != _Symbol) continue;
      if(PositionGetInteger(POSITION_MAGIC) != Magic_Number) continue;
      
      ENUM_POSITION_TYPE posType = (ENUM_POSITION_TYPE)PositionGetInteger(POSITION_TYPE);
      
      // Check if this is an opposite position
      if((posType == POSITION_TYPE_BUY && oppositeAction == "Buy") ||
         (posType == POSITION_TYPE_SELL && oppositeAction == "Sell")) {
         
         // Close the position
         g_trade.PositionClose(posTicket);
         if(Enable_Logging)
            Print("Closed opposite position #", posTicket);
         anyClosed = true;
      }
   }
   if(Enable_Logging && !anyClosed)
      Print("No opposite positions to close.");
}

//+------------------------------------------------------------------+
//| Execute trade based on signals                                   |
//+------------------------------------------------------------------+
void ExecuteTrade() {
   // Reset partial close flag
   g_partialClosed = false;
   
   // Get current prices
   double ask = SymbolInfoDouble(_Symbol, SYMBOL_ASK);
   double bid = SymbolInfoDouble(_Symbol, SYMBOL_BID);
   double point = SymbolInfoDouble(_Symbol, SYMBOL_POINT);
   
   // Calculate SL/TP levels
   double sl = 0, tp = 0;
   ENUM_ORDER_TYPE orderType;
   
   if(g_entrySignal.action == "Buy") {
      orderType = ORDER_TYPE_BUY;
      sl = ask - SL_Pips * 10 * point;
      tp = ask + TP_Pips * 10 * point;
   } else { // "Sell"
      orderType = ORDER_TYPE_SELL;
      sl = bid + SL_Pips * 10 * point;
      tp = bid - TP_Pips * 10 * point;
   }
   if(Enable_Logging)
      Print("ExecuteTrade: type=", (orderType==ORDER_TYPE_BUY?"BUY":"SELL"), 
            ", lot=", DoubleToString(Lot_Size, 2), ", sl=", DoubleToString(sl, _Digits), 
            ", tp=", DoubleToString(tp, _Digits));
   
   // Open position
   bool success = g_trade.PositionOpen(_Symbol, orderType, Lot_Size, 0, sl, tp, "FollowTrendBot");
   
   if(success) {
      if(Enable_Logging)
         Print("Opened ", g_entrySignal.action, " order at ", (orderType == ORDER_TYPE_BUY ? ask : bid),
               " with SL ", sl, " TP ", tp, 
               ", using entry signal id ", g_entrySignal.id, 
               " and trend id ", g_trendSignal.id);
   } else {
      Print("ERROR opening ", g_entrySignal.action, " order: lastError=", GetLastError(), 
            ", retcode=", g_trade.ResultRetcode(), 
            ", desc=", g_trade.ResultRetcodeDescription());
   }
}

//+------------------------------------------------------------------+
//| Mark entry signal as used via PUT request                        |
//+------------------------------------------------------------------+
void MarkEntryAsUsed() {
   uchar result[];
   uchar data[]; // empty body for PUT per current API contract
   string headers = "Authorization: Bearer " + API_Key + "\r\nContent-Type: application/json\r\n";
   string url = PUT_Endpoint_Base + IntegerToString(g_entrySignal.id);
   
   // Send PUT request
   string response_headers = "";
   int res = WebRequest("PUT", url, headers, 5000, data, result, response_headers);
   
   if(res == 200) {
      if(Enable_Logging) {
         Print("Marked entry signal ", g_entrySignal.id, " as used");
      }
       
      // Update local signal state
      g_entrySignal.used = true;
      UpdateLabelsText();
   } else {
      Print("Failed to mark entry as used: status=", res, 
            ", lastError=", GetLastError(), ", headers=", response_headers);
   }
}

//+------------------------------------------------------------------+
//| Create or update the on-chart labels for Trend and Entry         |
//+------------------------------------------------------------------+
void CreateOrUpdateLabels() {
   // Trend label
   if(ObjectFind(0, LABEL_TREND) == -1) {
      ObjectCreate(0, LABEL_TREND, OBJ_LABEL, 0, 0, 0);
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_CORNER, CORNER_LEFT_UPPER);
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_XDISTANCE, 10);
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_YDISTANCE, 20);
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_SELECTABLE, false);
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_BACK, true);
      ObjectSetString(0, LABEL_TREND, OBJPROP_FONT, "Arial");
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_FONTSIZE, 10);
   }
   // Entry label
   if(ObjectFind(0, LABEL_ENTRY) == -1) {
      ObjectCreate(0, LABEL_ENTRY, OBJ_LABEL, 0, 0, 0);
      ObjectSetInteger(0, LABEL_ENTRY, OBJPROP_CORNER, CORNER_LEFT_UPPER);
      ObjectSetInteger(0, LABEL_ENTRY, OBJPROP_XDISTANCE, 10);
      ObjectSetInteger(0, LABEL_ENTRY, OBJPROP_YDISTANCE, 40);
      ObjectSetInteger(0, LABEL_ENTRY, OBJPROP_SELECTABLE, false);
      ObjectSetInteger(0, LABEL_ENTRY, OBJPROP_BACK, true);
      ObjectSetString(0, LABEL_ENTRY, OBJPROP_FONT, "Arial");
      ObjectSetInteger(0, LABEL_ENTRY, OBJPROP_FONTSIZE, 10);
   }
}

//+------------------------------------------------------------------+
//| Update label texts and colors                                    |
//+------------------------------------------------------------------+
void UpdateLabelsText() {
   CreateOrUpdateLabels();
   string nowStr = TimeToString(TimeCurrent(), TIME_MINUTES|TIME_SECONDS);
   // Trend text
   string trendText = "Trend: ";
   color  trendColor = clrSilver;
   if(g_trendSignal.action != "") {
      trendText += g_trendSignal.action + "  id=" + IntegerToString(g_trendSignal.id) + 
                   "  price=" + DoubleToString(g_trendSignal.price, _Digits) + 
                   "  used=" + (g_trendSignal.used?"true":"false") + "  (" + nowStr + ")";
      trendColor = (g_trendSignal.used ? clrGray : (g_trendSignal.action=="Buy"? clrLime : clrTomato));
   } else {
      trendText += "(waiting)  (" + nowStr + ")";
      trendColor = clrSilver;
   }
   ObjectSetString(0, LABEL_TREND, OBJPROP_TEXT, trendText);
   ObjectSetInteger(0, LABEL_TREND, OBJPROP_COLOR, trendColor);
   
   // Entry text
   string entryText = "Entry: ";
   color  entryColor = clrSilver;
   if(g_entrySignal.action != "") {
      entryText += g_entrySignal.action + "  id=" + IntegerToString(g_entrySignal.id) + 
                   "  price=" + DoubleToString(g_entrySignal.price, _Digits) + 
                   "  used=" + (g_entrySignal.used?"true":"false") + "  (" + nowStr + ")";
      entryColor = (g_entrySignal.used ? clrGray : (g_entrySignal.action=="Buy"? clrLime : clrTomato));
   } else {
      entryText += "(waiting)  (" + nowStr + ")";
      entryColor = clrSilver;
   }
   ObjectSetString(0, LABEL_ENTRY, OBJPROP_TEXT, entryText);
   ObjectSetInteger(0, LABEL_ENTRY, OBJPROP_COLOR, entryColor);
}

//+------------------------------------------------------------------+
//| Manage open positions (partial close + breakeven)                |
//+------------------------------------------------------------------+
void ManageOpenPositions() {
   // Process each open position
   for(int i = PositionsTotal() - 1; i >= 0; i--) {
      ulong posTicket = PositionGetTicket(i);
      if(posTicket <= 0) continue;
      
      // Only manage positions for our symbol and magic number
      if(!PositionSelectByTicket(posTicket)) continue;
      if(PositionGetString(POSITION_SYMBOL) != _Symbol) continue;
      if(PositionGetInteger(POSITION_MAGIC) != Magic_Number) continue;
      
      // Skip if already partially closed
      if(g_partialClosed) continue;
      
      ENUM_POSITION_TYPE posType = (ENUM_POSITION_TYPE)PositionGetInteger(POSITION_TYPE);
      double openPrice = PositionGetDouble(POSITION_PRICE_OPEN);
      double currentPrice = PositionGetDouble(POSITION_PRICE_CURRENT);
      double posVolume = PositionGetDouble(POSITION_VOLUME);
      double point = SymbolInfoDouble(_Symbol, SYMBOL_POINT);
      double breakeven_pips = Y_Pips_Breakeven * 10 * point;
      
      // Check if we've reached the breakeven + partial close threshold
      bool hitTarget = false;
      
      if(posType == POSITION_TYPE_BUY && (currentPrice - openPrice >= breakeven_pips)) {
         hitTarget = true;
      }
      else if(posType == POSITION_TYPE_SELL && (openPrice - currentPrice >= breakeven_pips)) {
         hitTarget = true;
      }
      
      if(hitTarget) {
         // 1. Partial close half the position
         if(posVolume >= 0.02) { // Minimum to be able to close half
            double closeVolume = posVolume / 2;
            bool closeSuccess = g_trade.PositionClosePartial(posTicket, closeVolume);
            
            if(closeSuccess) {
               if(Enable_Logging)
                  Print("Partially closed position #", posTicket, " (", closeVolume, " lots)");
               
               // 2. Move SL to breakeven
               bool modifySuccess = g_trade.PositionModify(posTicket, openPrice, PositionGetDouble(POSITION_TP));
               
               if(modifySuccess) {
                  if(Enable_Logging)
                     Print("Modified SL to breakeven at ", openPrice);
                  
                  // Mark as processed
                  g_partialClosed = true;
               } else {
                  Print("Error modifying SL to breakeven: ", GetLastError());
               }
            } else {
               Print("Error partially closing position: ", GetLastError());
            }
         }
         else if(Enable_Logging) {
            Print("Reached breakeven price but volume too small for partial close: ", posVolume);
         }
      }
      else if(Enable_Logging) {
         double dist = (posType==POSITION_TYPE_BUY) ? (currentPrice - openPrice) : (openPrice - currentPrice);
         Print("Breakeven not reached yet. Progress=", DoubleToString(dist/(10*point), 1), " pips / target=", Y_Pips_Breakeven);
      }
   }
}

//+------------------------------------------------------------------+
//| Check for trade reversals based on entry signals                 |
//+------------------------------------------------------------------+
void CheckForReversals() {
   // Skip if no entry signal or signal is used
   if(g_entrySignal.action == "" || g_entrySignal.used || g_entrySignal.timestamp == 0)
      return;
   
   // Only check once per signal ID
   static int lastCheckedId = 0;
   if(lastCheckedId == g_entrySignal.id)
      return;
   
   lastCheckedId = g_entrySignal.id;
   if(Enable_Logging)
      Print("Checking reversals on new entry signal id=", g_entrySignal.id, " action=", g_entrySignal.action);
   
   // Look for positions to close due to signal reversal
   for(int i = PositionsTotal() - 1; i >= 0; i--) {
      ulong posTicket = PositionGetTicket(i);
      if(posTicket <= 0) continue;
      
      // Only manage positions for our symbol and magic number
      if(!PositionSelectByTicket(posTicket)) continue;
      if(PositionGetString(POSITION_SYMBOL) != _Symbol) continue;
      if(PositionGetInteger(POSITION_MAGIC) != Magic_Number) continue;
      
      ENUM_POSITION_TYPE posType = (ENUM_POSITION_TYPE)PositionGetInteger(POSITION_TYPE);
      
      // Check for reversal
      if((posType == POSITION_TYPE_BUY && g_entrySignal.action == "Sell") ||
         (posType == POSITION_TYPE_SELL && g_entrySignal.action == "Buy")) {
         
         // Close the position due to reversal
         if(g_trade.PositionClose(posTicket)) {
            if(Enable_Logging) {
               Print("Closed position #", posTicket, " due to ", g_entrySignal.action, " signal reversal");
            }
         } else {
            Print("Error closing position #", posTicket, ": ", GetLastError());
         }
      }
   }
}
