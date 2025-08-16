//+------------------------------------------------------------------+
//| FollowTrendBot.mq5                                               |
//| MT5 EA — Trend+Entry via API, PUT mark-used, reversals, v1.05    |
//| - Robust JSON parser, no-ref trim, PUT retry, normalize action   |
//+------------------------------------------------------------------+
#property copyright "FollowTrendBot"
#property version "1.05"
#property strict
#include <Trade/Trade.mqh>
#include <Arrays/ArrayObj.mqh>
//================== Inputs ==================
input string API_Key = "your_api_key_here";
input string API_Endpoint_Trend = "https://tradingsignals-ae14b4a15912.herokuapp.com/api/trend";
input string API_Endpoint_Entry = "https://tradingsignals-ae14b4a15912.herokuapp.com/api/entry";
input string PUT_Endpoint_Base = "https://tradingsignals-ae14b4a15912.herokuapp.com/api/ActiveSignals/markused/";
input double SL_Pips = 50.0;
input double TP_Pips = 100.0;
input double Y_Pips_Breakeven = 50.0;
input double MaxEntryPriceDiffPips = 25.0; // Max lệch so với signal price
input int Max_Slippage = 3; // In pips (đổi sang points)
input double Lot_Size = 0.01;
input int Magic_Number = 12345;
input bool Enable_Logging = true;
//================== Data ====================
struct SignalInfo {
   int id;
   string action; // "Buy" | "Sell"
   double price; // có thể 0 nếu API không set
   bool used;
   datetime timestamp;
};
CTrade g_trade;
SignalInfo g_trendSignal, g_entrySignal;
int g_apiErrors=0;
bool g_partialClosed=false;
datetime g_lastTrendCheck=0, g_lastEntryCheck=0;
// Chống mở trùng theo entry.id
int g_lastProcessedEntryId=0;
// Retry PUT
bool g_markPending=false;
int g_markPendingId=0;
datetime g_markLastAttempt=0;
// UI
string LABEL_TREND="FTB_TrendLabel";
string LABEL_ENTRY="FTB_EntryLabel";
//================== Utils ===================
double PipInPoints(){ if(_Digits==5 || _Digits==3) return 10.0; return 1.0; }
double PipsToPrice(double p){ return p * PipInPoints() * SymbolInfoDouble(_Symbol,SYMBOL_POINT); }
double PriceDiffInPips(double a,double b){ return MathAbs(a-b)/(SymbolInfoDouble(_Symbol,SYMBOL_POINT)*PipInPoints()); }
string PosTypeToStr(ENUM_POSITION_TYPE t){
   if(t==POSITION_TYPE_BUY) return "Buy";
   if(t==POSITION_TYPE_SELL) return "Sell";
   return "Other";
}
// --- Trim không tham chiếu (fix compile) ---
bool __is_sp(ushort c){ return (c==' ' || c=='\t' || c=='\r' || c=='\n'); }
string TrimAll(const string &s)
{
   int n=StringLen(s); if(n==0) return s;
   int i=0; while(i<n && __is_sp(StringGetCharacter(s,i))) i++;
   int j=n-1; while(j>=i && __is_sp(StringGetCharacter(s,j))) j--;
   if(j<i) return "";
   return StringSubstr(s,i,j-i+1);
}
string NormalizeAction(const string &sin)
{
   string s = TrimAll(sin);
   string up = s;
   StringToUpper(up);
   if(up=="BUY" || up=="LONG") return "Buy";
   if(up=="SELL"|| up=="SHORT") return "Sell";
   return ""; // unknown
}
// --- Robust JSON helpers (case-insensitive, tolerant) ---
int FindKeyCaseInsensitive(const string &jsonUp, const string &keyUp, int start=0)
{
   string pat="\""+keyUp+"\"";
   return StringFind(jsonUp, pat, start);
}
bool ExtractJsonStringValue(const string &json, const string &key, string &out)
{
   string jsonUp = json;
   StringToUpper(jsonUp);
   string keyUp = key;
   StringToUpper(keyUp);
   int pos = FindKeyCaseInsensitive(jsonUp, keyUp, 0);
   if(pos<0) return false;
   int colon = StringFind(json, ":", pos);
   if(colon<0) return false;
   // skip spaces
   int i = colon+1;
   while(i<StringLen(json))
   {
      ushort c=StringGetCharacter(json,i);
      if(!(c==' '||c=='\t'||c=='\r'||c=='\n')) break;
      i++;
   }
   if(i>=StringLen(json) || StringGetCharacter(json,i)!='"') return false;
   int q1=i;
   int q2=StringFind(json, "\"", q1+1);
   if(q2<0) return false;
   out = StringSubstr(json, q1+1, q2-(q1+1));
   return true;
}
bool ExtractJsonNumberValue(const string &json, const string &key, double &out)
{
   string jsonUp = json;
   StringToUpper(jsonUp);
   string keyUp = key;
   StringToUpper(keyUp);
   int pos = FindKeyCaseInsensitive(jsonUp, keyUp, 0);
   if(pos<0) return false;
   int colon = StringFind(json, ":", pos);
   if(colon<0) return false;
   int i=colon+1;
   while(i<StringLen(json))
   {
      ushort c=StringGetCharacter(json,i);
      if(!(c==' '||c=='\t'||c=='\r'||c=='\n')) break;
      i++;
   }
   string s="";
   for(; i<StringLen(json); i++)
   {
      ushort c=StringGetCharacter(json,i);
      if((c>='0' && c<='9') || c=='.' || c=='-' || c=='+') s+=ShortToString(c);
      else break;
   }
   if(s=="") return false;
   out = StringToDouble(s);
   return true;
}
bool ExtractJsonBoolValue(const string &json, const string &key, bool &out)
{
   string jsonUp = json;
   StringToUpper(jsonUp);
   string keyUp = key;
   StringToUpper(keyUp);
   int pos = FindKeyCaseInsensitive(jsonUp, keyUp, 0);
   if(pos<0) return false;
   int colon = StringFind(json, ":", pos);
   if(colon<0) return false;
   int i=colon+1;
   while(i<StringLen(json))
   {
      ushort c=StringGetCharacter(json,i);
      if(!(c==' '||c=='\t'||c=='\r'||c=='\n')) break;
      i++;
   }
   string temp4 = StringSubstr(json, i, 4);
   StringToUpper(temp4);
   string t4 = temp4;
   string temp5 = StringSubstr(json, i, 5);
   StringToUpper(temp5);
   string t5 = temp5;
   if(t4=="TRUE") { out=true; return true; }
   if(t5=="FALSE") { out=false; return true; }
   return false;
}
// Cắt object đầu tiên nếu response là mảng: [ { ... } , ... ]
string FirstObjectFromArray(const string &json)
{
   int lbrace = StringFind(json, "{");
   int rbrace = StringFind(json, "}", lbrace);
   if(lbrace>=0 && rbrace>lbrace)
      return StringSubstr(json, lbrace, rbrace-lbrace+1);
   return json; // fallback
}
// Forward declarations
void CreateOrUpdateLabels(); void UpdateLabelsText();
void GetTrendSignal(); void GetEntrySignal(); void ParseSignalJSON(string json, SignalInfo &signal);
void ProcessSignals(); void CloseOppositePositions(string action);
void ExecuteTrade(); bool MarkEntryAsUsedTry(int id, bool with_body);
void ManageOpenPositions(); void CheckForReversals();
//================= Lifecycle =================
int OnInit()
{
   g_trade.SetExpertMagicNumber(Magic_Number);
   g_trade.SetMarginMode();
   g_trade.SetTypeFillingBySymbol(_Symbol);
   g_trade.SetDeviationInPoints( (int)MathMax(1, MathRound(Max_Slippage * PipInPoints())) );
   g_trendSignal.id=0; g_trendSignal.action=""; g_trendSignal.price=0; g_trendSignal.used=true; g_trendSignal.timestamp=0;
   g_entrySignal.id=0; g_entrySignal.action=""; g_entrySignal.price=0; g_entrySignal.used=true; g_entrySignal.timestamp=0;
   EventSetTimer(10);
   CreateOrUpdateLabels(); UpdateLabelsText();
   if(Enable_Logging){
      Print("FollowTrendBot v1.05 on ",_Symbol," TF ",EnumToString(Period()),
            " | Digits=",_Digits," | PipInPoints=",DoubleToString(PipInPoints(),1),
            " | DeviationPoints=", (int)MathMax(1, MathRound(Max_Slippage * PipInPoints())));
      Print("Trend API: ",API_Endpoint_Trend," | Entry API: ",API_Endpoint_Entry);
   }
   GetTrendSignal(); GetEntrySignal(); UpdateLabelsText(); ProcessSignals();
   return(INIT_SUCCEEDED);
}
void OnDeinit(const int reason)
{
   EventKillTimer();
   ObjectDelete(0,LABEL_TREND); ObjectDelete(0,LABEL_ENTRY);
   if(Enable_Logging) Print("Stopped: ",reason);
}
void OnTimer()
{
   datetime now=TimeCurrent(); MqlDateTime dt; TimeToStruct(now,dt);
   // Trend: phút 00/30 hoặc >30'
   if(g_lastTrendCheck==0 || dt.min==0 || dt.min==30 || (now-g_lastTrendCheck)>1800){
      if(now-g_lastTrendCheck>=30){ g_lastTrendCheck=now; if(Enable_Logging) Print("Check TREND @",TimeToString(now)); GetTrendSignal(); }
   }
   // Entry: mỗi ≥60s
   if(g_lastEntryCheck==0 || (now-g_lastEntryCheck)>=60){
      g_lastEntryCheck=now; if(Enable_Logging) Print("Check ENTRY @",TimeToString(now)); GetEntrySignal(); ProcessSignals();
   }
   // Retry PUT nếu còn pending (15s/lần)
   if(g_markPending && (now - g_markLastAttempt >= 15)){
      if(Enable_Logging) Print("Retry PUT mark used for entry id=",g_markPendingId);
      bool ok = MarkEntryAsUsedTry(g_markPendingId, true);
      if(!ok) ok = MarkEntryAsUsedTry(g_markPendingId, false);
      if(ok){
         g_markPending=false;
         if(Enable_Logging) Print("PUT retry success for id=",g_markPendingId);
      }else{
         if(Enable_Logging) Print("PUT retry still failing for id=",g_markPendingId);
      }
   }
   ChartRedraw();
}
void OnTick()
{
   ManageOpenPositions();
   CheckForReversals();
}
//================= API ======================
void GetTrendSignal()
{
   string response="", hdr=""; uchar out[], in[];
   string heads="X-API-Key: "+API_Key+"\r\n";
   int res=WebRequest("GET", API_Endpoint_Trend, heads, 5000, in, out, hdr);
   if(res==200){
      response=CharArrayToString(out);
      if(Enable_Logging){
         string prev;
         if(StringLen(response)>180) prev = StringSubstr(response,0,180)+"...";
         else prev = response;
         Print("TREND 200, len=",StringLen(response),", preview=",prev);
      }
      ParseSignalJSON(response,g_trendSignal);
      UpdateLabelsText();
      g_apiErrors=0;
   } else {
      g_apiErrors++; int le=GetLastError();
      Print("Trend API failed: status=",res,", lastError=",le);
      if(le==4066 || le==4016) Print("Allow WebRequest: ",API_Endpoint_Trend);
      if(g_apiErrors>=3){ Print("Too many API errors -> remove EA"); ExpertRemove(); }
   }
}
void GetEntrySignal()
{
   string response="", hdr=""; uchar out[], in[];
   string heads="X-API-Key: "+API_Key+"\r\n";
   int res=WebRequest("GET", API_Endpoint_Entry, heads, 5000, in, out, hdr);
   if(res==200){
      response=CharArrayToString(out);
      if(Enable_Logging){
         string prev;
         if(StringLen(response)>180) prev = StringSubstr(response,0,180)+"...";
         else prev = response;
         Print("ENTRY 200, len=",StringLen(response),", preview=",prev);
      }
      ParseSignalJSON(response,g_entrySignal);
      UpdateLabelsText();
      g_apiErrors=0;
      CheckForReversals();
   } else {
      g_apiErrors++; int le=GetLastError();
      Print("Entry API failed: status=",res,", lastError=",le);
      if(le==4066 || le==4016) Print("Allow WebRequest: ",API_Endpoint_Entry);
      if(g_apiErrors>=3){ Print("Too many API errors -> remove EA"); ExpertRemove(); }
   }
}
// Parse tolerant (array/object, alias keys, case-insensitive)
void ParseSignalJSON(string json, SignalInfo &s)
{
   string j = TrimAll(json);
   if(StringLen(j)==0){ if(Enable_Logging) Print("ParseSignalJSON: empty response"); return; }
   // Nếu là array -> lấy object đầu
   if(StringGetCharacter(j,0)=='[') j = FirstObjectFromArray(j);
   // --- ID ---
   double idNum=0;
   if( ExtractJsonNumberValue(j,"id",idNum) ||
       ExtractJsonNumberValue(j,"Id",idNum) ||
       ExtractJsonNumberValue(j,"ID",idNum) )
      s.id=(int)idNum;
   // --- ACTION / DIRECTION ---
   string act="";
   if( ExtractJsonStringValue(j,"action",act) ||
       ExtractJsonStringValue(j,"Action",act) ||
       ExtractJsonStringValue(j,"direction",act) ||
       ExtractJsonStringValue(j,"side",act) ||
       ExtractJsonStringValue(j,"signal",act) )
      s.action = NormalizeAction(act);
   // --- PRICE ---
   double pr=0;
   if( ExtractJsonNumberValue(j,"price",pr) ||
       ExtractJsonNumberValue(j,"Price",pr) ||
       ExtractJsonNumberValue(j,"entryPrice",pr) ||
       ExtractJsonNumberValue(j,"signal_price",pr) )
      s.price=pr;
   // --- USED ---
   bool usedVal=false;
   bool haveUsed = ( ExtractJsonBoolValue(j,"used",usedVal) ||
                     ExtractJsonBoolValue(j,"Used",usedVal) ||
                     ExtractJsonBoolValue(j,"isUsed",usedVal) );
   if(haveUsed) s.used = usedVal;
   s.timestamp=TimeCurrent();
   if(Enable_Logging)
      Print("Parsed -> id:",s.id," act:",s.action," price:",DoubleToString(s.price,_Digits),
            " used:",(s.used?"true":"false"));
}
//================= CORE =====================
void ProcessSignals()
{
   if(Enable_Logging) Print("\n🔄 PROCESSING SIGNALS - Entry ID=", g_entrySignal.id, ", lastProcessed=", g_lastProcessedEntryId);
   
   // đủ hướng?
   if(g_trendSignal.action=="" || g_entrySignal.action==""){
      if(Enable_Logging) Print("❌ Skip: missing action(s). trend=",g_trendSignal.action,", entry=",g_entrySignal.action);
      return;
   }
   // normalize lần nữa cho chắc
   string trendAct = NormalizeAction(g_trendSignal.action);
   string entryAct = NormalizeAction(g_entrySignal.action);
   if(trendAct=="" || entryAct==""){
      if(Enable_Logging) Print("❌ Skip: unknown action(s). trend=",g_trendSignal.action,", entry=",g_entrySignal.action);
      return;
   }
   // khớp hướng
   if(trendAct != entryAct){
      if(Enable_Logging) Print("❌ Skip: mismatch. trend=",g_trendSignal.action,", entry=",g_entrySignal.action);
      return;
   }
   
   // Kiểm tra và log các lệnh đang mở (chỉ để thông tin)
   int openSameTypeCount = 0;
   for(int i=PositionsTotal()-1; i>=0; i--) {
      ulong tk = PositionGetTicket(i); if(tk<=0) continue;
      if(!PositionSelectByTicket(tk)) continue;
      if(PositionGetString(POSITION_SYMBOL) != _Symbol) continue;
      if((int)PositionGetInteger(POSITION_MAGIC) != Magic_Number) continue;
      
      ENUM_POSITION_TYPE t = (ENUM_POSITION_TYPE)PositionGetInteger(POSITION_TYPE);
      string posType = PosTypeToStr(t);
      
      if((t == POSITION_TYPE_BUY && entryAct == "Buy") || 
         (t == POSITION_TYPE_SELL && entryAct == "Sell")) {
         openSameTypeCount++;
         if(Enable_Logging) Print("ℹ️ Existing ", posType, " position #", tk, " found. Will open additional position.");
      }
   }
   
   // Không ngăn mở lệnh mới, chỉ log thông tin
   if(Enable_Logging && openSameTypeCount > 0) {
      Print("ℹ️ Already have ", openSameTypeCount, " open ", entryAct, " positions. Continuing with new entry signal.");
   }
   
   // Kiểm tra nếu ID trùng thì chỉ cần cách nhau tối thiểu 1 phút
   MqlDateTime now; TimeCurrent(now);
   datetime currentTime = TimeCurrent();
   datetime lastProcessedTime = g_entrySignal.timestamp;
   int timeDiffSeconds = (int)(currentTime - lastProcessedTime);
   
   // Chỉ kiểm tra nếu cùng ID và đã từng xử lý ID này trước đây
   if(g_entrySignal.id!=0 && g_entrySignal.id==g_lastProcessedEntryId && timeDiffSeconds < 60){
      if(Enable_Logging) Print("❌ Skip: entry id ",g_entrySignal.id," đã xử lý cách đây ", timeDiffSeconds, " giây (<60s)");
      return;
   }
   
   if(Enable_Logging && g_entrySignal.id==g_lastProcessedEntryId) {
      Print("⚠️ Tín hiệu có ID trùng với ID cũ nhưng đã quá 60 giây nên vẫn xử lý");
   }
   
   // nếu server đã used=true thì bỏ qua (an toàn)
   if(g_entrySignal.used){
      if(Enable_Logging) Print("❌ Skip: entry.used=true (id=",g_entrySignal.id,")");
      return;
   }
   
   if(Enable_Logging) Print("✅ Signal validation passed - continuing with trade entry");
   double cur;
   if(entryAct == "Buy") cur = SymbolInfoDouble(_Symbol, SYMBOL_ASK);
   else cur = SymbolInfoDouble(_Symbol, SYMBOL_BID);
   bool price_ok=true;
   if(g_entrySignal.price>0){
      double diff=PriceDiffInPips(cur, g_entrySignal.price);
      if(Enable_Logging) Print("Price chk diff=",DoubleToString(diff,1)," pips / limit=",MaxEntryPriceDiffPips);
      if(diff>MaxEntryPriceDiffPips) price_ok=false;
   }else if(Enable_Logging) Print("Signal price==0 → skip distance check");
   if(!price_ok){ if(Enable_Logging) Print("Skip: price too far"); return; }
   CloseOppositePositions(entryAct);
   ExecuteTrade();
}
void CloseOppositePositions(string action)
{
   if(Enable_Logging) Print("🔒 Checking for opposite positions to close before ", action, " entry");
   
   string opp=(action=="Buy"?"Sell":"Buy");
   bool any=false;
   int posCount = PositionsTotal();
   
   if(Enable_Logging) Print("Found ", posCount, " total positions");
   
   for(int i=posCount-1;i>=0;i--){
      ulong tk=PositionGetTicket(i); if(tk<=0) continue;
      if(!PositionSelectByTicket(tk)) continue;
      if(PositionGetString(POSITION_SYMBOL)!=_Symbol) continue;
      if((int)PositionGetInteger(POSITION_MAGIC)!=Magic_Number) continue;
      
      ENUM_POSITION_TYPE t=(ENUM_POSITION_TYPE)PositionGetInteger(POSITION_TYPE);
      string posType = PosTypeToStr(t);
      
      if(Enable_Logging) Print("Position #", tk, ": type=", posType, ", action=", action, ", opposite=", opp);
      
      bool shouldClose = false;
      if(t==POSITION_TYPE_BUY && opp=="Buy") shouldClose = true;
      if(t==POSITION_TYPE_SELL && opp=="Sell") shouldClose = true;
      
      if(shouldClose){
         if(Enable_Logging) Print("🔴 CLOSING opposite position #", tk, " (", posType, ") before ", action, " entry");
         if(g_trade.PositionClose(tk)){
            any=true;
            if(Enable_Logging) Print("✅ Successfully closed opposite #",tk);
         } else {
            Print("❌ Error closing opposite #",tk,": ",GetLastError(),", ret=",g_trade.ResultRetcode(),", ",g_trade.ResultRetcodeDescription());
         }
      }
   }
   
   if(Enable_Logging && !any) Print("✅ No opposite positions found, safe to open new ", action, " trade");
}
void ExecuteTrade()
{
   if(Enable_Logging) Print("💰 EXECUTING TRADE based on signals");
   
   g_partialClosed=false;
   double ask=SymbolInfoDouble(_Symbol,SYMBOL_ASK), bid=SymbolInfoDouble(_Symbol,SYMBOL_BID);
   double sl=0, tp=0; ENUM_ORDER_TYPE type;
   string act = NormalizeAction(g_entrySignal.action);
   
   if(act=="Buy"){
      type=ORDER_TYPE_BUY; 
      sl=ask-PipsToPrice(SL_Pips); 
      tp=ask+PipsToPrice(TP_Pips);
      if(Enable_Logging) Print("🔵 Preparing BUY order @ ", DoubleToString(ask, _Digits));
   }
   else{
      type=ORDER_TYPE_SELL; 
      sl=bid+PipsToPrice(SL_Pips); 
      tp=bid-PipsToPrice(TP_Pips);
      if(Enable_Logging) Print("🔴 Preparing SELL order @ ", DoubleToString(bid, _Digits));
   }
   
   if(Enable_Logging) {
      Print("Parameters: lot=",DoubleToString(Lot_Size,2),
           " SL=",DoubleToString(sl,_Digits)," TP=",DoubleToString(tp,_Digits),
           " slippage=", (int)MathMax(1, MathRound(Max_Slippage * PipInPoints())), " points");
   }
   
   bool ok=g_trade.PositionOpen(_Symbol,type,Lot_Size,0,sl,tp);
   if(ok){
      // Đánh dấu Id đã xử lý và gọi PUT API
      g_lastProcessedEntryId = g_entrySignal.id;
      g_entrySignal.used = true;
      UpdateLabelsText();
      
      if(Enable_Logging) Print("✅ ORDER OPENED SUCCESSFULLY! Ticket=", g_trade.ResultOrder(), ", Price=", g_trade.ResultPrice());
      if(Enable_Logging) Print("Marking entry signal id=", g_lastProcessedEntryId, " as USED via PUT API");
      
      bool mark_ok = MarkEntryAsUsedTry(g_lastProcessedEntryId, true);
      if(!mark_ok) {
         if(Enable_Logging) Print("Retrying PUT with empty body...");
         mark_ok = MarkEntryAsUsedTry(g_lastProcessedEntryId, false);
      }
      
      if(!mark_ok){
         g_markPending=true; 
         g_markPendingId=g_lastProcessedEntryId; 
         g_markLastAttempt=TimeCurrent();
         if(Enable_Logging) Print("⚠️ Mark used API failed, will retry later. id=",g_markPendingId);
      }
      
      if(Enable_Logging) {
         Print("📊 TRADE SUMMARY:");
         Print("- Action: ", act);
         Print("- Entry Signal: id=", g_entrySignal.id, ", price=", g_entrySignal.price);
         Print("- Trend Signal: id=", g_trendSignal.id, ", action=", g_trendSignal.action);
         Print("- Entry Price: ", g_trade.ResultPrice());
         Print("- Stop Loss: ", sl);
         Print("- Take Profit: ", tp);
      }
   } else {
      Print("❌ OPEN ORDER FAILED: lastErr=",GetLastError(),", ret=",g_trade.ResultRetcode(),", ",g_trade.ResultRetcodeDescription());
   }
}
// Trả true nếu PUT thành công (200/204)
bool MarkEntryAsUsedTry(int id, bool with_body)
{
   string response_headers = "";
   string headers = "X-API-Key: " + API_Key + "\r\nContent-Type: application/json\r\n";
   string url = PUT_Endpoint_Base + IntegerToString(id);
   uchar data[];
   if(with_body)
   {
      string body = "{\"used\":true}";
      int n = StringToCharArray(body, data, 0, -1, CP_UTF8);
      if(n > 0) ArrayResize(data, n - 1);
   }
   else
   {
      ArrayResize(data, 0);
   }
   uchar result[];
   g_markLastAttempt = TimeCurrent();
   int res = WebRequest("PUT", url, headers, 5000, data, result, response_headers);
   if(res == 200 || res == 204)
   {
      if(Enable_Logging) Print("PUT mark used OK (", res, ") for id=", id, " body=", (with_body ? "yes" : "no"));
      return true;
   }
   if(Enable_Logging) Print("PUT mark used FAIL status=", res, " for id=", id, " body=", (with_body ? "yes" : "no"));
   return false;
}
//================= UI =======================
void CreateOrUpdateLabels()
{
   if(ObjectFind(0,LABEL_TREND)==-1){
      ObjectCreate(0, LABEL_TREND, OBJ_LABEL, 0, 0, 0);
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_CORNER, CORNER_LEFT_UPPER);
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_XDISTANCE, 10);
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_YDISTANCE, 20);
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_SELECTABLE, false);
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_BACK, true);
      ObjectSetString(0, LABEL_TREND, OBJPROP_FONT, "Arial");
      ObjectSetInteger(0, LABEL_TREND, OBJPROP_FONTSIZE, 10);
   }
   if(ObjectFind(0,LABEL_ENTRY)==-1){
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
void UpdateLabelsText()
{
   CreateOrUpdateLabels();
   string nowStr=TimeToString(TimeCurrent(), TIME_MINUTES|TIME_SECONDS);
   string t="Trend: "; color tc=clrSilver;
   if(g_trendSignal.action!=""){
      t += g_trendSignal.action + " id=" + IntegerToString(g_trendSignal.id) +
           " price=" + DoubleToString(g_trendSignal.price,_Digits) +
           " used=" + (g_trendSignal.used ? "true" : "false") + " (" + nowStr + ")";
      if(g_trendSignal.used){
         tc = clrGray;
      }else{
         if(g_trendSignal.action=="Buy"){
            tc = clrLime;
         }else{
            tc = clrTomato;
         }
      }
   }else{
      t += "(waiting) (" + nowStr + ")";
   }
   ObjectSetString(0, LABEL_TREND, OBJPROP_TEXT, t);
   ObjectSetInteger(0, LABEL_TREND, OBJPROP_COLOR, tc);
   string e="Entry: "; color ec=clrSilver;
   if(g_entrySignal.action!=""){
      e += g_entrySignal.action + " id=" + IntegerToString(g_entrySignal.id) +
           " price=" + DoubleToString(g_entrySignal.price,_Digits) +
           " used=" + (g_entrySignal.used ? "true" : "false") + " (" + nowStr + ")";
      if(g_entrySignal.used){
         ec = clrGray;
      }else{
         if(g_entrySignal.action=="Buy"){
            ec = clrLime;
         }else{
            ec = clrTomato;
         }
      }
   }else{
      e += "(waiting) (" + nowStr + ")";
   }
   ObjectSetString(0, LABEL_ENTRY, OBJPROP_TEXT, e);
   ObjectSetInteger(0, LABEL_ENTRY, OBJPROP_COLOR, ec);
}
//=========== Position Management ============
void ManageOpenPositions()
{
   for(int i=PositionsTotal()-1;i>=0;i--){
      ulong tk=PositionGetTicket(i); if(tk<=0) continue;
      if(!PositionSelectByTicket(tk)) continue;
      if(PositionGetString(POSITION_SYMBOL)!=_Symbol) continue;
      if((int)PositionGetInteger(POSITION_MAGIC)!=Magic_Number) continue;
      if(g_partialClosed) continue;
      ENUM_POSITION_TYPE t=(ENUM_POSITION_TYPE)PositionGetInteger(POSITION_TYPE);
      double open=PositionGetDouble(POSITION_PRICE_OPEN);
      double cur =PositionGetDouble(POSITION_PRICE_CURRENT);
      double vol =PositionGetDouble(POSITION_VOLUME);
      bool hit=false;
      if(t==POSITION_TYPE_BUY && (cur-open)>=PipsToPrice(Y_Pips_Breakeven)) hit=true;
      if(t==POSITION_TYPE_SELL && (open-cur)>=PipsToPrice(Y_Pips_Breakeven)) hit=true;
      if(hit){
         if(vol>=0.02){
            double half=vol/2.0;
            if(g_trade.PositionClosePartial(tk,half)){
               if(Enable_Logging) Print("Partial closed #",tk," ",DoubleToString(half,2)," lots");
               double tp=PositionGetDouble(POSITION_TP);
               if(g_trade.PositionModify(tk, open, tp)){
                  if(Enable_Logging) Print("Moved SL to BE @",DoubleToString(open,_Digits));
                  g_partialClosed=true;
               }else{
                  Print("Modify BE error: ",GetLastError(),", ret=",g_trade.ResultRetcode(),", ",g_trade.ResultRetcodeDescription());
               }
            }else{
               Print("Partial close error #",tk,": ",GetLastError());
            }
         }else if(Enable_Logging) Print("Hit BE but volume too small: ",DoubleToString(vol,2));
      }else if(Enable_Logging){
         double prog=PriceDiffInPips(cur,open);
         Print("BE not reached: ",DoubleToString(prog,1)," / target=",Y_Pips_Breakeven," pips");
      }
   }
}
// ===== Đảo chiều: luôn đóng lệnh, không phụ thuộc used =====
void CheckForReversals()
{
   if(g_entrySignal.action=="" || g_entrySignal.timestamp==0) return;
   
   // Bỏ kiểm tra lastCheckedId - luôn kiểm tra mỗi khi được gọi
   // static int lastCheckedId=0;
   // if(lastCheckedId==g_entrySignal.id) return;
   // lastCheckedId=g_entrySignal.id;
   
   if(Enable_Logging) Print("⚠️ REVERSAL CHECK: Entry signal=",g_entrySignal.action," id=",g_entrySignal.id);
   string act = NormalizeAction(g_entrySignal.action);
   
   if(act == "") {
      if(Enable_Logging) Print("⚠️ Invalid action after normalize: ", g_entrySignal.action);
      return;
   }
   
   if(Enable_Logging) Print("🔍 Checking for opposite positions to ", act, " signal");
   int posCount = PositionsTotal();
   if(Enable_Logging) Print("Found ", posCount, " total positions");
   
   bool anyPositionClosed = false;
   
   for(int i=posCount-1; i>=0; i--){
      ulong tk=PositionGetTicket(i); if(tk<=0) continue;
      if(!PositionSelectByTicket(tk)) continue;
      if(PositionGetString(POSITION_SYMBOL)!=_Symbol) continue;
      if((int)PositionGetInteger(POSITION_MAGIC)!=Magic_Number) continue;
      
      ENUM_POSITION_TYPE t=(ENUM_POSITION_TYPE)PositionGetInteger(POSITION_TYPE);
      string posType = PosTypeToStr(t);
      
      if(Enable_Logging) Print("Position #", tk, ": type=", posType, ", entry signal=", act);
      
      bool shouldClose = false;
      if(t==POSITION_TYPE_BUY && act=="Sell") shouldClose = true;
      if(t==POSITION_TYPE_SELL && act=="Buy") shouldClose = true;
      
      if(shouldClose){
         if(Enable_Logging) Print("🔴 CLOSING position #", tk, " (", posType, ") due to opposite signal: ", act);
         if(g_trade.PositionClose(tk)){
            anyPositionClosed = true;
            if(Enable_Logging) Print("✅ Successfully closed #",tk);
         }else{
            Print("❌ Error closing position #",tk,": ",GetLastError(),", ret=",g_trade.ResultRetcode(),", ",g_trade.ResultRetcodeDescription());
         }
      } else {
         if(Enable_Logging) Print("✅ Position #", tk, " (", posType, ") matches signal (", act, ") - keeping open");
      }
   }
   
   // Reset g_lastProcessedEntryId khi đóng bất kỳ lệnh nào để cho phép vào lệnh mới ngay lập tức
   if(anyPositionClosed) {
      if(Enable_Logging) Print("🔄 Resetting lastProcessedEntryId from ", g_lastProcessedEntryId, " to 0 after closing positions");
      g_lastProcessedEntryId = 0;
   }
}