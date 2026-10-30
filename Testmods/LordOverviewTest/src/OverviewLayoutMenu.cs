using System;
using System.Collections.Generic;
using System.Linq;
using Noesis;
namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private static ComboBox designChoice,sortChoice,lordGridChoice,valueGridChoice;
  private static Button sortDirection;
  private static StackPanel cardOptions;
  private static ScrollViewer menuScroll,orderScroll;
  private static bool updatingMenu;
  private static string orderSignature="",appliedLayout="",lordChoiceSignature="",valueChoiceSignature="";
  private static readonly List<Metric?> sortMetrics=new List<Metric?>();
  private static GridFlow[] lordChoices=new GridFlow[0],valueChoices=new GridFlow[0];
  private static bool LayoutDropDownOpen=>designChoice?.IsDropDownOpen==true || sortChoice?.IsDropDownOpen==true || lordGridChoice?.IsDropDownOpen==true || valueGridChoice?.IsDropDownOpen==true;
  private static void CloseLayoutDropDowns(){foreach(var combo in new[]{designChoice,sortChoice,lordGridChoice,valueGridChoice})if(combo!=null)combo.IsDropDownOpen=false;}
  private static FrameworkElement BuildLayoutMenu(){
   var controls=new StackPanel();
   controls.Children.Add(MenuLine(Text("Design"),designChoice=MenuCombo()));
   designChoice.ItemsSource=new[]{Text("DesignList"),Text("DesignSplitList"),Text("DesignCards")};Tip(designChoice,Text("DesignHelp"));
   designChoice.SelectionChanged+=(_,__)=>{if(updatingMenu)return;int selected=designChoice.SelectedIndex;if(selected<0 || selected>2)return;
    UiAction(()=>{settings.SetDisplayDesign((OverviewDesign)selected);ApplyLayout();})();};
   cardOptions=new StackPanel();
   cardOptions.Children.Add(MenuLine(Text("LordGrid"),lordGridChoice=MenuCombo()));Tip(lordGridChoice,Text("GridHelp"));
   cardOptions.Children.Add(MenuLine(Text("ValueGrid"),valueGridChoice=MenuCombo()));Tip(valueGridChoice,Text("GridHelp"));
   lordGridChoice.SelectionChanged+=(_,__)=>ChooseGrid(true);valueGridChoice.SelectionChanged+=(_,__)=>ChooseGrid(false);controls.Children.Add(cardOptions);
   sortChoice=MenuCombo();Tip(sortChoice,Text("SortByHelp"));controls.Children.Add(MenuLine(Text("SortBy"),sortChoice));
   sortDirection=ActionButton("",Text("SortDirectionHelp"),()=>{state.Select(state.Column);signature="";Render();UpdateColumnsPopup();});controls.Children.Add(sortDirection);
   sortChoice.SelectionChanged+=(_,__)=>{if(updatingMenu)return;int index=sortChoice.SelectedIndex;if(index<0 || index>=sortMetrics.Count)return;var metric=sortMetrics[index];
    UiAction(()=>{if(metric.HasValue && !selection.Columns.Contains(metric.Value))return;if(state.Column!=metric){state.Select(metric);signature="";Render();}UpdateColumnsPopup();})();};
   orderScroll=new ScrollViewer {HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=52};Tip(orderScroll,Text("HeaderDragHint"));controls.Children.Add(MenuLine(Text("ValueOrder"),orderScroll));
   return controls;
  }
  private static ComboBox MenuCombo()=>new ComboBox {FontSize=14,HorizontalAlignment=HorizontalAlignment.Stretch};
  private static FrameworkElement MenuLine(string title,FrameworkElement control){
   var line=new StackPanel {Margin=new Thickness(2,1,2,5)};var label=Label(title);label.FontSize=13;label.TextWrapping=TextWrapping.Wrap;line.Children.Add(label);line.Children.Add(control);return line;
  }
  private static void ChooseGrid(bool lords){
   if(updatingMenu)return;var combo=lords?lordGridChoice:valueGridChoice;var choices=lords?lordChoices:valueChoices;int index=combo.SelectedIndex;
   if(index<0 || index>=choices.Length)return;var selected=choices[index];
   UiAction(()=>{settings.SetDisplayGrid(lords,selected.Normalize(lords?rows.Count:selection.Columns.Length));ApplyLayout();})();
  }
  private static void NormalizeGridPreferences(int lordCount,int valueCount){
   if(lordCount>0){var normalized=settings.DisplayLordGrid.Normalize(lordCount);if(normalized.ToString()!=settings.DisplayLordGrid.ToString())settings.SetDisplayGrid(true,normalized);}
   var values=settings.DisplayValueGrid.Normalize(valueCount);if(values.ToString()!=settings.DisplayValueGrid.ToString())settings.SetDisplayGrid(false,values);
  }
  private static void ApplyLayout(){
   string next=settings.DisplayDesign+"/"+settings.DisplayLordGrid+"/"+settings.DisplayValueGrid;
   if(next==appliedLayout){UpdateColumnsPopup();return;}appliedLayout=next;
   CancelHeaderGesture();CancelMenuDrag();System.Threading.Interlocked.Increment(ref generation);signature="";
   if(panel?.IsVisible==true){Render();RequestRefresh();}UpdateColumnsPopup();PositionSettingsMenu();
  }
  private static void UpdateLayoutMenu(){
   if(designChoice==null)return;updatingMenu=true;
   try {
    designChoice.SelectedIndex=(int)settings.DisplayDesign;cardOptions.Visibility=settings.DisplayDesign==OverviewDesign.Cards?Visibility.Visible:Visibility.Collapsed;
    UpdateGridReadouts();
    sortMetrics.Clear();sortMetrics.Add(null);sortMetrics.AddRange(selection.Columns.Select(m=>(Metric?)m));
    var labels=new List<string>{Text("Player")};labels.AddRange(selection.Columns.Select(MetricName));sortChoice.ItemsSource=labels;sortChoice.SelectedIndex=sortMetrics.IndexOf(state.Column);
    sortDirection.Content=Label(Text(state.Descending?"Descending":"Ascending"));
    string next=Metrics.Serialize(selection.Columns)+"/"+catalogRevision;
    if(orderSignature!=next){
     CancelHeaderGesture();RemoveHeaders(true);orderScroll.Content=null;
     var grid=new Grid();for(int i=0;i<=selection.Columns.Length;i++)grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
     var surface=NewHeaderSurface(grid,1,"menu/order/");grid.Children.Add(CreateHeader(surface,null,Text("Player"),Label("#")));
     var columns=selection.Columns;for(int i=0;i<columns.Length;i++){var header=CreateHeader(surface,columns[i],MetricHelp(columns[i]),MetricIcon(columns[i]));Grid.SetColumn(header,i+1);grid.Children.Add(header);}
     orderScroll.Content=grid;orderSignature=next;UpdateHeaderEditing();
    }
   } finally {updatingMenu=false;}
  }
  private static void UpdateGridReadouts(){
   if(lordGridChoice==null)return;bool before=updatingMenu;updatingMenu=true;
   try {
    UpdateGridChoice(lordGridChoice,rows.Count,settings.DisplayLordGrid,ref lordChoices,ref lordChoiceSignature);
    UpdateGridChoice(valueGridChoice,selection.Columns.Length,settings.DisplayValueGrid,ref valueChoices,ref valueChoiceSignature);
   } finally {updatingMenu=before;}
  }
  private static void UpdateGridChoice(ComboBox combo,int count,GridFlow selected,ref GridFlow[] choices,ref string cached){
   string key=count+"/"+selected;if(cached==key)return;cached=key;var effective=selected.Normalize(count);choices=GridFlow.Choices(count,selected);
   combo.ItemsSource=choices.Select(c=>c.Label).ToArray();combo.SelectedIndex=Array.FindIndex(choices,c=>c.ToString()==effective.ToString());
  }
  private static void ResetLayoutMenu(){designChoice=sortChoice=lordGridChoice=valueGridChoice=null;sortDirection=null;cardOptions=null;menuScroll=orderScroll=null;sortMetrics.Clear();orderSignature=appliedLayout=lordChoiceSignature=valueChoiceSignature="";lordChoices=valueChoices=new GridFlow[0];updatingMenu=false;}
 }
}
