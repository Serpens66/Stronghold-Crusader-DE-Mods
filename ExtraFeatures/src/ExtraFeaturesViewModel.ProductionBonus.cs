using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using Noesis;
using System;
using System.Globalization;
using System.Threading;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesViewModel
    {
        private int fixedProductionBonusPercent=50,effectiveFixedProductionBonusPercent=50;
        private volatile bool fixedProductionBonusMissionLocked;
        private bool productionBonusResolvedValid=true;
        private string productionBonusIssueText="",productionBonusWarningText="";
        private RelayCommand productionBonusDetailsCommand;
        public bool CanConfigureProductionGoods=>CanEditHostSettings&&!fixedProductionBonusMissionLocked;
        public bool CanConfigureFixedProductionBonus=>CanEditHostSettings&&!fixedProductionBonusMissionLocked;
        public string FixedProductionBonusTitle=>SerpLocalization.Get("ExtraFeatures.ProductionBonus.Title");
        public string FixedProductionBonusHelp=>SerpLocalization.Get("ExtraFeatures.ProductionBonus.Help");
        public string EffectiveFixedProductionBonusText=>productionBonusResolvedValid?string.Format(CultureInfo.CurrentCulture,
            SerpLocalization.Get("ExtraFeatures.ProductionBonus.Effective"),effectiveFixedProductionBonusPercent.ToString("+0;-0;0",CultureInfo.InvariantCulture)):
            SerpLocalization.Get("ExtraFeatures.ProductionBonus.EffectiveInvalid");
        public string ProductionBonusIssueText=>productionBonusIssueText;
        public string ProductionBonusWarningText=>productionBonusWarningText.Length==0?productionBonusIssueText:productionBonusWarningText+
            (productionBonusIssueText.Length==0?"":" "+productionBonusIssueText);
        public Visibility ProductionBonusIssueVisibility=>productionBonusIssueText.Length==0?Visibility.Collapsed:Visibility.Visible;
        public Visibility ProductionBonusWarningVisibility=>ProductionBonusWarningText.Length==0?Visibility.Collapsed:Visibility.Visible;
        public string ProductionBonusDetailsText=>SerpLocalization.Get("ExtraFeatures.ProductionBonus.Details");
        public string ProductionBonusDetailsHelp=>SerpLocalization.Get("ExtraFeatures.ProductionBonus.DetailsHelp");
        public RelayCommand ProductionBonusDetailsCommand=>productionBonusDetailsCommand??(productionBonusDetailsCommand=new RelayCommand(
            ()=>ProductionBonusDetailsRequested?.Invoke()));
        internal event Action ProductionBonusDetailsRequested;
        [SyncHostOnly]
        public int FixedProductionBonusPercent
        {
            get=>fixedProductionBonusPercent;
            set{if(!fixedProductionBonusMissionLocked||IsApplyingMissionSettingsSnapshot)
                SetIntSetting(ref fixedProductionBonusPercent,value,-100,1000,nameof(FixedProductionBonusPercent),nameof(FixedProductionBonusValueText));}
        }
        public string FixedProductionBonusValueText
        {
            get=>FixedProductionBonusPercent.ToString(CultureInfo.InvariantCulture);
            set{if(int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int parsed))FixedProductionBonusPercent=parsed;
                OnPropertyChanged(nameof(FixedProductionBonusValueText));}
        }
        // Called only through the runtime's coalesced shared Unity presentation callback.
        internal void UpdateProductionBonusPresentation(int effective,string issue,string warning,bool valid=true)
        {
            productionBonusResolvedValid=valid;effectiveFixedProductionBonusPercent=effective;productionBonusIssueText=issue??"";productionBonusWarningText=warning??"";
            OnPropertyChanged(nameof(EffectiveFixedProductionBonusText));OnPropertyChanged(nameof(ProductionBonusIssueText));
            OnPropertyChanged(nameof(ProductionBonusIssueVisibility));OnPropertyChanged(nameof(ProductionBonusWarningText));
            OnPropertyChanged(nameof(ProductionBonusWarningVisibility));
        }
        internal void SetFixedProductionBonusMissionLocked(bool locked)
        {
            if(fixedProductionBonusMissionLocked==locked)return;fixedProductionBonusMissionLocked=locked;
            Action notify = () => { OnPropertyChanged(nameof(CanConfigureFixedProductionBonus)); OnPropertyChanged(nameof(CanConfigureProductionGoods)); };
            if(Thread.CurrentThread.ManagedThreadId==uiThread)notify();
            else if(uiContext!=null)uiContext.Post(_=>notify(),null);
        }
    }
}
