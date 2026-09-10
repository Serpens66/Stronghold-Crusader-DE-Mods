from __future__ import annotations

import unittest
import tkinter as tk

from atlas_builder.gui import AtlasBuilderApp, GroupDialog, Tooltip, group_context_text
from atlas_builder.i18n import TEXT
from atlas_builder.models import GroupConfig


class FakeWidget:
    def __init__(self) -> None:
        self.bindings: dict[str, object] = {}
        self.cancelled: list[str] = []

    def bind(self, event, callback, add=None):
        self.bindings[event] = callback

    def after(self, _delay, _callback):
        return "scheduled"

    def after_cancel(self, value):
        self.cancelled.append(value)


class TooltipTests(unittest.TestCase):
    def test_lifecycle_bindings_and_pending_callback_cleanup(self) -> None:
        widget = FakeWidget()
        tooltip = Tooltip(widget, "help")
        self.assertEqual(set(widget.bindings), {"<Enter>", "<Leave>", "<ButtonPress>", "<Destroy>"})
        tooltip._schedule()
        self.assertEqual(tooltip.after_id, "scheduled")
        tooltip.hide()
        self.assertEqual(widget.cancelled, ["scheduled"])
        self.assertIsNone(tooltip.after_id)

    def test_context_help_changes_with_group_options_and_language(self) -> None:
        basic = group_context_text("de", "anim_castle", "none", "target-pixel-anchor", "reject")
        advanced = group_context_text("de", "anim_castle", "none", "source-metadata", "source-metadata")
        english = group_context_text("en", "anim_castle", "none", "source-metadata", "source-metadata")
        self.assertIn("Loader-Maximalindex 138", basic)
        self.assertIn("sichere Standard", basic)
        self.assertIn("AssetRipper-JSONs", advanced)
        self.assertIn("metadata validation", english)
        self.assertNotEqual(basic, advanced)
        self.assertNotEqual(advanced, english)

    def test_real_gui_language_context_and_tooltip_smoke(self) -> None:
        try:
            app = AtlasBuilderApp()
        except tk.TclError as exc:
            self.skipTest(f"Tk display unavailable: {exc}")
        try:
            app.withdraw()
            tooltip = app.add_button._atlas_builder_tooltip
            tooltip._show()
            app.update_idletasks()
            self.assertIsNotNone(tooltip.window)

            app.language_var.set("en")
            app._refresh_language()
            app.update_idletasks()
            self.assertIsNone(tooltip.window)
            self.assertEqual(app.add_button.cget("text"), "Add")

            dialog = GroupDialog(app, GroupConfig("anim_castle", "", "none"))
            dialog.withdraw()
            dialog.pivot_mode_label_var.set(app.tr("source-metadata"))
            dialog.missing_policy_label_var.set(app.tr("missing-source-metadata"))
            dialog._update_metadata_state()
            self.assertIn("AssetRipper JSON", dialog.context_var.get())
            dialog.destroy()
        finally:
            app.destroy()


class TranslationTests(unittest.TestCase):
    def test_german_and_english_keys_are_complete(self) -> None:
        self.assertEqual(set(TEXT["de"]), set(TEXT["en"]))


if __name__ == "__main__":
    unittest.main()
