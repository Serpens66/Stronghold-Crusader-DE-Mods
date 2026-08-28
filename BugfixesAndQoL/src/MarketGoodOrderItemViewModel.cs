// Feature: Presents one movable goods slot in the lobby settings editor.
using Noesis;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;

namespace BugfixesAndQoL
{
    public sealed class MarketGoodOrderItemViewModel : LobbyModSettingsBaseViewModel
    {
        private readonly Action<int, int> moveRequested;
        private int position;
        private int goodId;
        private string goodName = string.Empty;
        private ImageSource icon;

        internal MarketGoodOrderItemViewModel(Action<int, int> moveRequested)
        {
            this.moveRequested = moveRequested ?? throw new ArgumentNullException(nameof(moveRequested));
            MovePreviousCommand = new RelayCommand(() => this.moveRequested(GoodId, -1));
            MoveNextCommand = new RelayCommand(() => this.moveRequested(GoodId, 1));
        }

        public RelayCommand MovePreviousCommand { get; }
        public RelayCommand MoveNextCommand { get; }
        public int GoodId => goodId;
        public string SearchKey => "bugfixes.market-good." + goodId;
        public string SearchTitle => goodName;
        public ImageSource Icon => icon;
        public string PositionText => (position + 1).ToString();
        public string GoodToolTip => SerpLocalization.Get(
            SerpLocalization.MarketGoodsOrderPositionHelp,
            "Position", PositionText,
            "Good", goodName);
        public string MovePreviousToolTip => SerpLocalization.Get(
            SerpLocalization.MarketGoodsOrderMovePreviousHelp,
            "Good", goodName);
        public string MoveNextToolTip => SerpLocalization.Get(
            SerpLocalization.MarketGoodsOrderMoveNextHelp,
            "Good", goodName);

        internal void Update(int newPosition, int newGoodId, string newGoodName, ImageSource newIcon)
        {
            position = newPosition;
            goodId = newGoodId;
            goodName = newGoodName ?? string.Empty;
            icon = newIcon;
            OnPropertyChanged(nameof(GoodId));
            OnPropertyChanged(nameof(SearchKey));
            OnPropertyChanged(nameof(SearchTitle));
            OnPropertyChanged(nameof(Icon));
            OnPropertyChanged(nameof(PositionText));
            OnPropertyChanged(nameof(GoodToolTip));
            OnPropertyChanged(nameof(MovePreviousToolTip));
            OnPropertyChanged(nameof(MoveNextToolTip));
        }
    }
}
