from pathlib import Path
root=Path.cwd(); p=root/'BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/FormationStartupTests.cs'; s=p.read_text()
s=s.replace('public static class SerpLocalization { public static string Get(string key)=>key=="BugfixesAndQoL.ArrangementTooltip"?"Arrangement":key; }','public static class SerpLocalization { public static string Language="en"; public static string Get(string key)=>key=="BugfixesAndQoL.ArrangementTooltip"?(Language=="de"?"Aufstellung":"Arrangement"):key; }')
s=s.replace('namespace Noesis {\n','''namespace Noesis {
    public enum Visibility { Visible,Hidden,Collapsed }
    public class MouseEventArgs : EventArgs {}
    public delegate void MouseEventHandler(object sender,MouseEventArgs args);
    public class TextBlock { public Visibility Visibility=Visibility.Hidden; }
    public class Button {
        private MouseEventHandler enter,leave;
        public int EnterHandlers,LeaveHandlers;
        public event MouseEventHandler MouseEnter { add {enter+=value;EnterHandlers++;} remove {enter-=value;EnterHandlers--;} }
        public event MouseEventHandler MouseLeave { add {leave+=value;LeaveHandlers++;} remove {leave-=value;LeaveHandlers--;} }
        public void Enter()=>enter?.Invoke(this,new MouseEventArgs());
        public void Leave()=>leave?.Invoke(this,new MouseEventArgs());
    }
''',1)
start=s.index('        public int HoverEnters,HoverLeaves;'); end=s.index('        public event PropertyChangedEventHandler',start)
s=s[:start]+'''        public HUD_Troops HUDTroopPanel=new HUD_Troops();
        public string TroopsPanelRollover_AmountReq1,TroopsPanelRollover_AmountGot1;
        public object TroopsPanelRollover_GoodsImage1;
'''+s[end:]
pos=s.index('    public static class Enums')
s=s[:pos]+'''    public class HUD_Troops {
        public Noesis.Button OpenButton=new Noesis.Button();
        public Noesis.TextBlock RefTroopsPanelRollover=new Noesis.TextBlock(),RefTroopsPanelRollover2=new Noesis.TextBlock();
        public object FindName(string name)=>name=="BugfixesAndQoLFormationOpenButton"?OpenButton:null;
    }
'''+s[pos:]
start=s.index('            menu.ShowButtonTooltipCommand.Execute(null);'); end=s.index('            var converter=',start)
s=s[:start]+'''            var panel=main.HUDTroopPanel;var button=panel.OpenButton;
            menu.RefreshHostState();menu.RefreshHostState();
            Require(button.EnterHandlers==1 && button.LeaveHandlers==1,"Duplicate direct handlers");
            main.TroopsPanelRollover_AmountReq1="cost";main.TroopsPanelRollover_AmountGot1="body";main.TroopsPanelRollover_GoodsImage1=new object();
            panel.RefTroopsPanelRollover2.Visibility=Noesis.Visibility.Visible;button.Enter();
            Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Visible && panel.RefTroopsPanelRollover2.Visibility==Noesis.Visibility.Hidden && main.TroopsPanelRollover=="Arrangement" && !menu.RolloverVisible,"Direct event did not show short Vanilla rollover");
            Require(main.TroopsPanelRollover_AmountReq1=="" && main.TroopsPanelRollover_AmountGot1=="" && main.TroopsPanelRollover_GoodsImage1==null,"Old costs leaked");
            button.Leave();Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Hidden,"Leave did not hide");
            SerpLocalization.Language="de";button.Enter();Require(main.TroopsPanelRollover=="Aufstellung","German hover missing");SerpLocalization.Language="en";
            main.TroopsPanelRollover="Different Vanilla button";button.Leave();
            Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Visible,"Foreign short hover hidden");
            button.Enter();main.TroopsPanelRollover="Knight";panel.RefTroopsPanelRollover.Visibility=Noesis.Visibility.Hidden;panel.RefTroopsPanelRollover2.Visibility=Noesis.Visibility.Visible;button.Leave();
            Require(panel.RefTroopsPanelRollover2.Visibility==Noesis.Visibility.Visible,"Knight hover hidden");
            button.Enter();menu.CloseMenu();Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Hidden,"Close did not hide");
            button.Enter();FormationRuntime.Last.Failed=true;menu.RefreshHostState();
            Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Hidden && button.EnterHandlers==0,"Runtime failure left hover/handlers");
            FormationRuntime.Last.Failed=false;menu.RefreshHostState();button.Enter();
            CrusaderDE.FatControler.currentScene=CrusaderDE.Enums.SceneIDS.Other;menu.RefreshHostState();
            Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Hidden && button.EnterHandlers==0,"Scene change left hover/handlers");
            CrusaderDE.FatControler.currentScene=CrusaderDE.Enums.SceneIDS.ActualMainGame;menu.RefreshHostState();button.Enter();
            main.Show_HUD_Troops=false;main.Notify("Show_HUD_Troops");
            Require(panel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Hidden && button.EnterHandlers==0,"Deselection left hover/handlers");
            main.Show_HUD_Troops=true;menu.RefreshHostState();button.Enter();
            var replacement=new CrusaderDE.HUD_Troops();main.HUDTroopPanel=replacement;main.TroopsPanelRollover="Replacement button";replacement.RefTroopsPanelRollover.Visibility=Noesis.Visibility.Visible;
            menu.RefreshHostState();menu.RefreshHostState();
            Require(button.EnterHandlers==0 && button.LeaveHandlers==0 && replacement.OpenButton.EnterHandlers==1 && replacement.OpenButton.LeaveHandlers==1,"HUD replacement handlers incorrect");
            Require(replacement.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Visible,"Replacement hover hidden");
            button.Enter();Require(main.TroopsPanelRollover=="Replacement button","Old button still active");
            replacement.OpenButton.Enter();Require(main.TroopsPanelRollover=="Arrangement","New HUD hover inactive");replacement.OpenButton.Leave();
            var newMain=new CrusaderDE.MainViewModel();CrusaderDE.MainViewModel.Instance=newMain;menu.RefreshHostState();
            Require(replacement.OpenButton.EnterHandlers==0 && newMain.HUDTroopPanel.OpenButton.EnterHandlers==1,"Viewmodel replacement handlers incorrect");
            newMain.HUDTroopPanel.OpenButton.Enter();main.Show_HUD_Troops=false;main.Notify("Show_HUD_Troops");
            Require(newMain.HUDTroopPanel.RefTroopsPanelRollover.Visibility==Noesis.Visibility.Visible,"Old viewmodel subscription remained");
            newMain.HUDTroopPanel.OpenButton.Leave();
'''+s[end:]
p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
