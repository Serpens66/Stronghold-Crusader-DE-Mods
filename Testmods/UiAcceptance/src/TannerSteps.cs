using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared;
using CrusaderDE;
using Noesis;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

using Path = System.IO.Path;

namespace UiAcceptance
{
    // The Tannery's slider as a player uses it: real worker, cows, mouse input and save.
    internal static unsafe class TannerSteps
    {
        private const string Panel = "ImprovedHuntersTannerPanel";
        private const string SliderName = "ImprovedHuntersTannerSlider";
        private const string MeatRunsName = "ImprovedHuntersTannerMeatRuns";
        private const string GoodRunsName = "ImprovedHuntersTannerGoodRuns";
        private const string ControlsName = "ImprovedHuntersTannerControls";
        // Vanilla's workshop product buttons begin this far above the panel's bottom (FletchersPanel: 28 + 57).
        private const float VanillaButtonsTop = 85f;
        // Vanilla's Chore 33 carries one byte. No Vanilla good: the value another mod could choose.
        private const int ForeignGood = 80;
        private const int ModeRva = 0x8574B90;
        private const int ProductivityRva = 0x379CEEC;
        private const int PlayerRecordStride = 0x583C;

        private static string savePath;

        internal static void Register()
        {
            Runner.Register("Tanner.Setup", Setup);
            Runner.Register("Tanner.Open", Open);
            Runner.Register("Tanner.StopEatingMeat", StopEatingMeat);
            Runner.Register("Tanner.SetSlider", SetSlider);
            Runner.Register("Tanner.DragSlider", DragSlider);
            Runner.Register("Tanner.DragRepeatedly", DragRepeatedly);
            Runner.Register("Tanner.SetForeignGood", SetForeignGood);
            Runner.Register("Tanner.ExpectForeignGood", ExpectForeignGood);
            Runner.Register("Tanner.LogBonusInputs", LogBonusInputs);
            Runner.Register("Tanner.ExpectPanel", ExpectPanel);
            Runner.Register("Tanner.Cycles", Cycles);
            Runner.Register("Tanner.Save", Save);
            Runner.Register("Tanner.Reload", Reload);
        }

        private static GameBuilding* FindTannery(int owner, out int id)
        {
            id = 0;
            SimpleNativeArray<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding* building = buildings.GetValuePointer(index);
                if (building->r_AliveState != AliveState.None && building->r_PlayerIdOwner == owner &&
                    building->r_BuildingType == eStructs.STRUCT_TANNERS_WORKSHOP)
                {
                    id = index + 1;
                    return building;
                }
            }
            return null;
        }

        private static int CountUnits(int owner, eChimps type)
        {
            int count = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.IsAlive && unit->r_ControllableForPlayerId == owner && unit->r_UnitChimp == type)
                    count++;
            }
            return count;
        }

        private static void PlaceFixture(int local)
        {
            lock (HuntSteps.Engine())
            {
                Runner.Require(UnitAccess.TryGetById(GamePlayerManagerAPI.Instance.GetLordUnitId(local), out GameUnit* lord, out _), "no local lord");
                int x = lord->r_CurrentTilePositionX, y = lord->r_CurrentTilePositionY;
                if (!HuntSteps.TryFindBuilding(local, eStructs.STRUCT_GRANARY, out _, out _))
                    Runner.Require(HuntSteps.Place(local, x, y, eMappers.MAPPER_GRANARY, eStructs.STRUCT_GRANARY, 8, out _, out _), "no free granary site");
                if (!HuntSteps.TryFindBuilding(local, eStructs.STRUCT_ARMOURY, out _, out _))
                    Runner.Require(HuntSteps.Place(local, x, y, eMappers.MAPPER_ARMOURY, eStructs.STRUCT_ARMOURY, 12, out _, out _), "no free armoury site");
                // Cows live only as part of a farm: a cow without one deletes itself in its first update (0x158780).
                // This map's start is desert, where a farm is normally not allowed.
                Runner.Require(HuntSteps.Place(local, x, y, eMappers.MAPPER_CATTLEFARM, eStructs.STRUCT_CATTLEFARM, 24, out _, out _, bypassGroundRules: true), "no free cattle farm site");
                Runner.Require(HuntSteps.Place(local, x, y, eMappers.MAPPER_TANNER, eStructs.STRUCT_TANNERS_WORKSHOP, 16, out int tx, out int ty), "no free Tannery site");
                AcceptLog.Info("TANNER_SETUP at=" + tx + "," + ty);
            }
        }

        private static IEnumerable<Wait> Setup(Step step)
        {
            int local = HuntSteps.Local();
            PlaceFixture(local);
            yield return new Until("a Tanner works there", () =>
            {
                lock (HuntSteps.Engine()) return CountUnits(local, eChimps.CHIMP_TYPE_TANNER) >= 1;
            });
        }

        private static (int Id, int X, int Y) TanneryPosition(int local)
        {
            lock (HuntSteps.Engine())
            {
                GameBuilding* tannery = FindTannery(local, out int id);
                Runner.Require(tannery != null, "no Tannery");
                return (id, tannery->r_TilePositionXBegin, tannery->r_TilePositionYBegin);
            }
        }

        // Opens the panel through the game's own map click, at any camera position.
        private static IEnumerable<Wait> Open(Step step)
        {
            (int id, int x, int y) = TanneryPosition(HuntSteps.Local());
            // A click right after a load is sometimes not read; AutomatisierungTest.md notes the open cause.
            for (int attempt = 0; attempt < 4 && !IsOpen(id); attempt++)
            {
                foreach (Wait wait in InputSteps.ClickTile(x + 1, y + 1)) yield return wait;
                yield return new Delay(1500);
            }
            yield return new Until("Tannery panel opened", () => IsOpen(id));
            AcceptLog.Info("TANNER_OPEN building=" + id + " panel=" + GameData.Instance.lastGameState.app_sub_mode);
        }

        private static bool IsOpen(int id) =>
            GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.in_structure == id;

        // Vanilla's granary toggle; the population would otherwise eat the delivered meat while it is counted.
        private static IEnumerable<Wait> StopEatingMeat(Step step)
        {
            EngineInterface.GameAction(Enums.GameActionCommand.SetFoodEaten, GameData.Instance.lastGameState.in_structure, 2);
            yield return new Delay(500);
        }

        private static Slider FindSlider()
        {
            var slider = GameXAMLManagerAPI.Instance.FindGlobalElement(SliderName) as Slider;
            Runner.Require(slider != null && slider.IsVisible && slider.ActualWidth > 0, "Tannery slider not shown");
            return slider;
        }

        private static Thumb FindThumb(DependencyObject node)
        {
            if (node is Thumb thumb) return thumb;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                Thumb found = FindThumb(VisualTreeHelper.GetChild(node, i));
                if (found != null) return found;
            }
            return null;
        }

        private static Point SliderPoint(Slider slider, int value)
        {
            Thumb thumb = FindThumb(slider);
            Runner.Require(thumb != null && thumb.ActualWidth > 0, "Tannery slider thumb missing");
            float x = (float)thumb.ActualWidth / 2 + ((float)slider.ActualWidth - (float)thumb.ActualWidth) * value / (float)slider.Maximum;
            return slider.PointToScreen(new Point(x, (float)slider.ActualHeight / 2));
        }

        // A thumb moves only when the frame after a mouse event has laid it out, and its next delta is
        // measured from there. At top game speed a frame can take longer than a person's next mouse event
        // here, so wait for rendered frames, not only for time.
        private static IEnumerable<Wait> InputPause()
        {
            yield return new Delay(60);
            yield return NextFrame.Instance;
            yield return NextFrame.Instance;
            yield return NextFrame.Instance;
        }

        private static string SliderState(Slider slider)
        {
            Thumb thumb = FindThumb(slider);
            return "value=" + slider.Value + " dragging=" + thumb.IsDragging + " over=" + slider.IsMouseOver + " thumbOver=" + thumb.IsMouseOver;
        }

        // Clicks the track beside the thumb, through the game's real Noesis input: one step per click.
        // Halfway to the next value: the thumb's centre reaches past the track's ends, where nothing is hit.
        private static IEnumerable<Wait> SetSlider(Step step)
        {
            foreach (Wait wait in WithoutRealMouse(ClickSlider(step))) yield return wait;
        }

        // NoesisView sends the real cursor to the UI whenever it moves (UpdateMouse), so anyone touching
        // the mouse during a run would drag the captured thumb to the cursor and fail the step at random.
        private static IEnumerable<Wait> WithoutRealMouse(IEnumerable<Wait> input)
        {
            NoesisView view = FatControler.instance.NGview;
            view.EnableMouse = false;
            try
            {
                foreach (Wait wait in input) yield return wait;
            }
            finally
            {
                view.EnableMouse = true;
            }
        }

        private static IEnumerable<Wait> ClickSlider(Step step)
        {
            int value = step.Int(0);
            Slider slider = FindSlider();
            NoesisView input = FatControler.instance.NGview;
            for (int click = 0; click < 3 && (int)Math.Round(slider.Value) != value; click++)
            {
                int current = (int)Math.Round(slider.Value);
                Point thumb = SliderPoint(slider, current), next = SliderPoint(slider, value < current ? current - 1 : current + 1);
                int x = (int)((thumb.X + next.X) / 2), y = (int)thumb.Y;
                input.MouseMove(x, y);
                foreach (Wait wait in InputPause()) yield return wait;
                AcceptLog.Info("TANNER_CLICK at=" + x + "," + y + " " + SliderState(slider));
                input.MouseButtonDown(x, y, MouseButton.Left);
                foreach (Wait wait in InputPause()) yield return wait;
                input.MouseButtonUp(x, y, MouseButton.Left);
                foreach (Wait wait in InputPause()) yield return wait;
                AcceptLog.Info("TANNER_CLICK up " + SliderState(slider));
            }
            yield return new Until("slider at " + value, () => (int)Math.Round(FindSlider().Value) == value);
            AcceptLog.Info("TANNER_SLIDER click value=" + value + " noesisHasKeyboard=" + GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard());
            yield return new Delay(500);
        }

        // Drags the thumb, as most players will.
        private static IEnumerable<Wait> DragSlider(Step step)
        {
            foreach (Wait wait in WithoutRealMouse(DragThumb(step))) yield return wait;
        }

        private static IEnumerable<Wait> DragThumb(Step step)
        {
            int value = step.Int(0);
            Slider slider = FindSlider();
            Point from = SliderPoint(slider, (int)Math.Round(slider.Value)), to = SliderPoint(slider, value);
            NoesisView input = FatControler.instance.NGview;
            input.MouseMove((int)from.X, (int)from.Y);
            foreach (Wait wait in InputPause()) yield return wait;
            input.MouseButtonDown((int)from.X, (int)from.Y, MouseButton.Left);
            foreach (Wait wait in InputPause()) yield return wait;
            AcceptLog.Info("TANNER_DRAG down from=" + (int)from.X + " to=" + (int)to.X + " " + SliderState(slider));
            for (int part = 1; part <= 4; part++)
            {
                input.MouseMove((int)(from.X + (to.X - from.X) * part / 4), (int)to.Y);
                foreach (Wait wait in InputPause()) yield return wait;
            }
            AcceptLog.Info("TANNER_DRAG moved " + SliderState(slider));
            input.MouseButtonUp((int)to.X, (int)to.Y, MouseButton.Left);
            yield return new Until("slider dragged to " + value, () => (int)Math.Round(FindSlider().Value) == value);
            AcceptLog.Info("TANNER_SLIDER drag value=" + value + " noesisHasKeyboard=" + GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard());
            yield return new Delay(500);
        }

        // A slider that only sometimes reacts is a broken slider. args: rounds; ends where it began.
        private static IEnumerable<Wait> DragRepeatedly(Step step)
        {
            int start = (int)Math.Round(FindSlider().Value);
            for (int round = 0; round < step.Int(0); round++)
            {
                foreach (int value in new[] { (start + 2) % 4, (start + 1) % 4, start })
                {
                    foreach (Wait wait in DragSlider(new Step { Id = step.Id + "-" + round, Args = new[] { value.ToString() } })) yield return wait;
                }
            }
            AcceptLog.Info("TANNER_DRAG_REPEATED rounds=" + step.Int(0));
        }

        // What a mod adding goods to the Tannery would do: Vanilla's own synchronized workshop action.
        private static IEnumerable<Wait> SetForeignGood(Step step)
        {
            EngineInterface.GameAction(Enums.GameActionCommand.SetNextWeaponMade, GameData.Instance.lastGameState.in_structure, ForeignGood);
            int local = HuntSteps.Local();
            yield return new Until("foreign good chosen", () => HasForeignGood(local));
            AcceptLog.Info("TANNER_FOREIGN_GOOD set=" + ForeignGood);
        }

        private static bool HasForeignGood(int local)
        {
            lock (HuntSteps.Engine())
            {
                GameBuilding* tannery = FindTannery(local, out _);
                return tannery != null && (int)tannery->r_ProducedGoodId == ForeignGood;
            }
        }

        private static IEnumerable<Wait> ExpectForeignGood(Step step)
        {
            Runner.Require(HasForeignGood(HuntSteps.Local()), "the slider must leave the workshop's produced good alone");
            AcceptLog.Info("TANNER_FOREIGN_GOOD kept=" + ForeignGood);
            yield break;
        }

        // Inputs of Vanilla's yield helper 0x18D940, pinned by the full-file hash of this test mod's native audit.
        private static IEnumerable<Wait> LogBonusInputs(Step step)
        {
            ReadBonusInputs(out int mode, out int productivity);
            AcceptLog.Info("TANNER_BONUS_INPUTS mode=" + mode + " productivity=" + productivity);
            yield break;
        }

        private static void ReadBonusInputs(out int mode, out int productivity)
        {
            byte* image = (byte*)GetModuleHandle("CrusaderDE.dll");
            Runner.Require(image != null, "CrusaderDE.dll not loaded");
            mode = *(int*)(image + ModeRva);
            productivity = *(int*)(image + ProductivityRva + HuntSteps.Local() * PlayerRecordStride);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);

        // args: hidden | slider value, 0 only meat to 3 no meat.
        private static IEnumerable<Wait> ExpectPanel(Step step)
        {
            string expected = step.Arg(0);
            bool visible = expected != "hidden";
            yield return new Until("Tannery panel " + expected, () =>
            {
                FrameworkElement panel = GameXAMLManagerAPI.Instance.FindGlobalElement(Panel);
                if (panel == null || panel.IsVisible != visible)
                    return false;
                if (!visible)
                    return true;
                int value = int.Parse(expected);
                var slider = GameXAMLManagerAPI.Instance.FindGlobalElement(SliderName) as Slider;
                var meat = GameXAMLManagerAPI.Instance.FindGlobalElement(MeatRunsName) as TextBlock;
                var good = GameXAMLManagerAPI.Instance.FindGlobalElement(GoodRunsName) as TextBlock;
                return slider != null && meat != null && good != null && (int)Math.Round(slider.Value) == value &&
                    meat.Text == (3 - value).ToString() && good.Text == value.ToString();
            });
            if (visible)
            {
                // Mods adding goods to the Tannery put their buttons where Vanilla's workshops have them.
                FrameworkElement panel = GameXAMLManagerAPI.Instance.FindGlobalElement(Panel);
                FrameworkElement controls = GameXAMLManagerAPI.Instance.FindGlobalElement(ControlsName);
                float buttonsTop = panel.PointToScreen(new Point(0, (float)panel.ActualHeight - VanillaButtonsTop)).Y;
                float controlsEnd = controls.PointToScreen(new Point(0, (float)controls.ActualHeight)).Y;
                AcceptLog.Info("TANNER_LAYOUT buttonsTop=" + buttonsTop + " controlsEnd=" + controlsEnd);
                Runner.Require(controlsEnd <= buttonsTop + 1f, "the slider covers the place of Vanilla's product buttons");
            }
            AcceptLog.Info("TANNER_PANEL " + expected);
        }

        // A cycle ends when no hide is left and the Tanner carries nothing and is not producing (AI states 5, 6).
        private static bool ReadCycle(int local, out int armour, out int meat, out string detail)
        {
            lock (HuntSteps.Engine())
            {
                armour = GamePlayerManagerAPI.Instance.GetGoodAmount(local, eGoods.STORED_LEATHER_ARMOUR);
                meat = GamePlayerManagerAPI.Instance.GetGoodAmount(local, eGoods.STORED_FOOD_MEAT);
                GameBuilding* tannery = FindTannery(local, out _);
                GameUnit* tanner = null;
                SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
                for (int index = 0; index < units.Length && tanner == null; index++)
                {
                    GameUnit* unit = units.GetValuePointer(index);
                    if (unit->r_AliveState == AliveState.IsAlive && unit->r_ControllableForPlayerId == local && unit->r_UnitChimp == eChimps.CHIMP_TYPE_TANNER)
                        tanner = unit;
                }
                if (tannery == null || tanner == null)
                {
                    detail = "no Tannery or Tanner";
                    return false;
                }
                detail = "hides=" + tannery->r_CowHidesAmount + " state=" + tanner->r_AIState + " carry=" + tanner->r_CarryGoodAmount +
                    " cows=" + CountUnits(local, eChimps.CHIMP_TYPE_COW) + " good=" + tannery->r_ProducedGoodId + " latched=" + tannery->r_NextProducedGoodId;
                return tannery->r_CowHidesAmount == 0 && tanner->r_CarryGoodAmount == 0 && tanner->r_AIState != 5 && tanner->r_AIState != 6;
            }
        }

        // args: expected armour and meat in total. Both include Vanilla's bonus, whose fractions carry
        // over on the Tanner (0x18D940), so the amounts must match what the scenario's order predicts.
        private static IEnumerable<Wait> Cycles(Step step)
        {
            int local = HuntSteps.Local(), expectedArmour = step.Int(0), expectedMeat = step.Int(1);
            int startArmour = 0, startMeat = 0, lastLog = Environment.TickCount;
            string detail = "";
            yield return new Until("Tanner between cows", () => ReadCycle(local, out startArmour, out startMeat, out _));
            int armour = 0, meat = 0;
            yield return new Until(expectedArmour + " armour and " + expectedMeat + " meat", () =>
            {
                bool between = ReadCycle(local, out armour, out meat, out detail);
                armour -= startArmour;
                meat -= startMeat;
                if (Environment.TickCount - lastLog > 5000)
                {
                    lastLog = Environment.TickCount;
                    AcceptLog.Info("TANNER_PROGRESS armour=" + armour + " meat=" + meat + " " + detail);
                }
                return between && armour >= expectedArmour && meat >= expectedMeat;
            });
            AcceptLog.Info("TANNER_RESULT armour=" + armour + " meat=" + meat + " expected=" + expectedArmour + "/" + expectedMeat + " " + detail);
            Runner.Require(armour == expectedArmour && meat == expectedMeat,
                "Tannery delivered " + armour + " armour and " + meat + " meat; expected " + expectedArmour + " and " + expectedMeat);
        }

        private static IEnumerable<Wait> Save(Step step)
        {
            string directory = Path.GetFullPath(ConfigSettings.GetSavesPath());
            string path = Path.Combine(directory, "TannerCheck.sav");
            var location = CameraControls2D.instance.getScreenXYForSaveCentring(
                (int)((float)(UnityEngine.Screen.width / 64) / PerfectPixelWithZoom.instance.getZoom()),
                (int)((float)(UnityEngine.Screen.height / 16) / PerfectPixelWithZoom.instance.getZoom()));
            int centreX = -1, centreY = -1;
            GameMap.instance.getRotationCentre(ref centreX, ref centreY);
            Runner.Require(EngineInterface.SaveSaveGame(path, location.x, location.y, centreX, centreY, false, false, false), "native save writer reported failure");
            Runner.Require(File.Exists(path) && new FileInfo(path).Length > 0, "native save file missing");
            savePath = path;
            AcceptLog.Info("TANNER_SAVE path=" + path);
            yield return new Delay(500);
        }

        private static IEnumerable<Wait> Reload(Step step)
        {
            Runner.Require(savePath != null, "no Tannery save");
            foreach (Wait wait in GameSteps.LoadSave(new Step { Id = step.Id + "-native", Args = new[] { savePath } })) yield return wait;
        }
    }
}
