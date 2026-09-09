from __future__ import annotations

import queue
import threading
import traceback
from datetime import datetime
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from .core import AtlasBuilderError, build_project, existing_output_groups, prepare_project
from .gm_groups import SUPPORTED_GROUPS
from .game_locator import find_game_data_directory
from .i18n import translate
from .models import GroupConfig, ModInfoConfig, ProjectConfig


DEFAULT_GAME_DATA = find_game_data_directory()


class GroupDialog(tk.Toplevel):
    def __init__(self, parent: "AtlasBuilderApp", value: GroupConfig | None = None):
        super().__init__(parent)
        self.parent = parent
        self.result: GroupConfig | None = None
        self.transient(parent)
        self.grab_set()
        self.title(parent.tr("edit") if value else parent.tr("add"))
        value = value or GroupConfig("", "", "none", None, "auto")
        self.gm_var = tk.StringVar(value=value.gm_file_name)
        self.colour_var = tk.StringVar(value=value.colour_directory)
        self.mask_var = tk.StringVar(value=value.mask_directory or "")
        self.prefix_var = tk.StringVar(value=value.source_prefix)

        frame = ttk.Frame(self, padding=12)
        frame.grid(sticky="nsew")
        self.columnconfigure(0, weight=1)
        frame.columnconfigure(1, weight=1)
        self._row(frame, 0, parent.tr("gm_group"), ttk.Combobox(
            frame, textvariable=self.gm_var, values=sorted(SUPPORTED_GROUPS, key=str.casefold), state="readonly", width=45
        ))
        self._path_row(frame, 1, parent.tr("colour_dir"), self.colour_var)
        self.mask_labels = {
            parent.tr(mode): mode for mode in ("none", "same-directory", "separate-directory")
        }
        self.mask_mode_label_var = tk.StringVar(value=parent.tr(value.mask_mode))
        mode_box = ttk.Combobox(
            frame,
            values=tuple(self.mask_labels),
            textvariable=self.mask_mode_label_var,
            state="readonly",
            width=30,
        )
        self._row(frame, 2, parent.tr("mask_mode"), mode_box)
        self._path_row(frame, 3, parent.tr("mask_dir"), self.mask_var)
        self._row(frame, 4, parent.tr("source_prefix"), ttk.Entry(frame, textvariable=self.prefix_var, width=48))
        buttons = ttk.Frame(frame)
        buttons.grid(row=5, column=0, columnspan=3, pady=(12, 0), sticky="e")
        ttk.Button(buttons, text="OK", command=self.accept).pack(side="left", padx=4)
        ttk.Button(buttons, text=parent.tr("cancel"), command=self.destroy).pack(side="left")
        self.bind("<Return>", lambda _event: self.accept())
        self.bind("<Escape>", lambda _event: self.destroy())

    @staticmethod
    def _row(frame, row: int, label: str, widget) -> None:
        ttk.Label(frame, text=label).grid(row=row, column=0, sticky="w", padx=(0, 8), pady=4)
        widget.grid(row=row, column=1, columnspan=2, sticky="ew", pady=4)

    def _path_row(self, frame, row: int, label: str, variable: tk.StringVar) -> None:
        ttk.Label(frame, text=label).grid(row=row, column=0, sticky="w", padx=(0, 8), pady=4)
        ttk.Entry(frame, textvariable=variable).grid(row=row, column=1, sticky="ew", pady=4)
        ttk.Button(frame, text=self.parent.tr("browse"), command=lambda: self._browse(variable)).grid(
            row=row, column=2, padx=(6, 0), pady=4
        )

    def _browse(self, variable: tk.StringVar) -> None:
        selected = filedialog.askdirectory(parent=self)
        if selected:
            variable.set(selected)

    def accept(self) -> None:
        if not self.gm_var.get():
            messagebox.showerror(self.parent.tr("error"), self.parent.tr("select_group"), parent=self)
            return
        self.result = GroupConfig(
            gm_file_name=self.gm_var.get(),
            colour_directory=self.colour_var.get().strip(),
            mask_mode=self.mask_labels[self.mask_mode_label_var.get()],
            mask_directory=self.mask_var.get().strip() or None,
            source_prefix=self.prefix_var.get() or "auto",
        )
        self.destroy()


class AtlasBuilderApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.project = ProjectConfig(target_game_data=DEFAULT_GAME_DATA)
        self.work_queue: queue.Queue[tuple[str, object]] = queue.Queue()
        self.language_var = tk.StringVar(value="de")
        self.game_data_var = tk.StringVar(value=DEFAULT_GAME_DATA)
        self.output_var = tk.StringVar()
        self.manifest_var = tk.BooleanVar(value=True)
        self.guid_var = tk.StringVar(value="MyAtlasMod")
        self.name_var = tk.StringVar(value="My Atlas Mod")
        self.author_var = tk.StringVar()
        self.version_var = tk.StringVar(value="1.0.0")
        self.description_var = tk.StringVar(value="Sprite atlas replacements for SHCDE.")
        self.website_var = tk.StringVar()
        self.gap_var = tk.StringVar(value="2")
        self.maximum_size_var = tk.StringVar(value="8192")
        self.status_var = tk.StringVar(value=self.tr("ready"))
        self._build_ui()
        self.after(100, self._poll_queue)
        self.minsize(920, 620)

    def tr(self, key: str, **values) -> str:
        return translate(self.language_var.get(), key, **values)

    def _build_ui(self) -> None:
        self.title(self.tr("title"))
        root = ttk.Frame(self, padding=10)
        root.pack(fill="both", expand=True)
        root.columnconfigure(1, weight=1)
        root.rowconfigure(5, weight=1)

        toolbar = ttk.Frame(root)
        toolbar.grid(row=0, column=0, columnspan=3, sticky="ew", pady=(0, 8))
        for key, command in (("new", self.new_project), ("open", self.open_project), ("save", self.save_project), ("save_as", self.save_project_as)):
            ttk.Button(toolbar, text=self.tr(key), command=command).pack(side="left", padx=(0, 5))
        ttk.Label(toolbar, text=self.tr("language")).pack(side="right", padx=(6, 0))
        language = ttk.Combobox(toolbar, textvariable=self.language_var, values=("de", "en"), width=5, state="readonly")
        language.pack(side="right")
        language.bind("<<ComboboxSelected>>", lambda _event: self._refresh_language())

        self._path_row(root, 1, "game_data", self.game_data_var)
        self._path_row(root, 2, "output_mod", self.output_var)

        manifest = ttk.LabelFrame(root, text=self.tr("manifest"), padding=8)
        manifest.grid(row=3, column=0, columnspan=3, sticky="ew", pady=8)
        for column in range(8):
            manifest.columnconfigure(column, weight=1 if column % 2 else 0)
        ttk.Checkbutton(manifest, variable=self.manifest_var, text=self.tr("manifest")).grid(row=0, column=0, columnspan=8, sticky="w")
        fields = (
            ("guid", self.guid_var),
            ("mod_name", self.name_var),
            ("author", self.author_var),
            ("version", self.version_var),
            ("description", self.description_var),
            ("website", self.website_var),
        )
        for index, (key, variable) in enumerate(fields):
            column = (index % 2) * 4
            row = 1 + index // 2
            ttk.Label(manifest, text=self.tr(key)).grid(row=row, column=column, sticky="w", padx=(0, 5), pady=3)
            ttk.Entry(manifest, textvariable=variable).grid(row=row, column=column + 1, columnspan=3, sticky="ew", padx=(0, 10), pady=3)

        packing = ttk.Frame(root)
        packing.grid(row=4, column=0, columnspan=3, sticky="ew", pady=(0, 8))
        ttk.Label(packing, text=self.tr("gap")).pack(side="left")
        ttk.Spinbox(packing, from_=2, to=64, textvariable=self.gap_var, width=6).pack(side="left", padx=(5, 20))
        ttk.Label(packing, text=self.tr("maximum_size")).pack(side="left")
        ttk.Combobox(
            packing,
            values=(2048, 4096, 8192),
            textvariable=self.maximum_size_var,
            width=8,
        ).pack(side="left", padx=5)

        groups_frame = ttk.LabelFrame(root, text=self.tr("groups"), padding=8)
        groups_frame.grid(row=5, column=0, columnspan=3, sticky="nsew")
        groups_frame.rowconfigure(0, weight=1)
        groups_frame.columnconfigure(0, weight=1)
        self.group_tree = ttk.Treeview(groups_frame, columns=("gm", "colour", "mask", "prefix"), show="headings")
        for name, width in (("gm", 180), ("colour", 360), ("mask", 170), ("prefix", 110)):
            self.group_tree.heading(name, text=self.tr(name + "_column"))
            self.group_tree.column(name, width=width, stretch=True)
        self.group_tree.grid(row=0, column=0, sticky="nsew")
        scroll = ttk.Scrollbar(groups_frame, orient="vertical", command=self.group_tree.yview)
        scroll.grid(row=0, column=1, sticky="ns")
        self.group_tree.configure(yscrollcommand=scroll.set)
        group_buttons = ttk.Frame(groups_frame)
        group_buttons.grid(row=1, column=0, columnspan=2, sticky="w", pady=(8, 0))
        self.add_button = ttk.Button(group_buttons, text=self.tr("add"), command=self.add_group)
        self.edit_button = ttk.Button(group_buttons, text=self.tr("edit"), command=self.edit_group)
        self.remove_button = ttk.Button(group_buttons, text=self.tr("remove"), command=self.remove_group)
        for button in (self.add_button, self.edit_button, self.remove_button):
            button.pack(side="left", padx=(0, 5))

        bottom = ttk.Frame(root)
        bottom.grid(row=6, column=0, columnspan=3, sticky="ew", pady=(8, 0))
        ttk.Label(bottom, textvariable=self.status_var).pack(side="left", fill="x", expand=True)
        self.progress = ttk.Progressbar(bottom, mode="indeterminate", length=130)
        self.progress.pack(side="left", padx=8)
        self.validate_button = ttk.Button(bottom, text=self.tr("validate"), command=self.validate_project)
        self.build_button = ttk.Button(bottom, text=self.tr("build"), command=self.build_atlases)
        self.validate_button.pack(side="right", padx=(5, 0))
        self.build_button.pack(side="right")
        ttk.Label(root, text=self.tr("material_note"), foreground="#805000").grid(row=7, column=0, columnspan=3, sticky="w", pady=(8, 0))

    def _path_row(self, parent, row: int, label_key: str, variable: tk.StringVar) -> None:
        ttk.Label(parent, text=self.tr(label_key)).grid(row=row, column=0, sticky="w", padx=(0, 8), pady=3)
        ttk.Entry(parent, textvariable=variable).grid(row=row, column=1, sticky="ew", pady=3)
        ttk.Button(parent, text=self.tr("browse"), command=lambda: self._browse(variable)).grid(row=row, column=2, padx=(6, 0), pady=3)

    def _browse(self, variable: tk.StringVar) -> None:
        selected = filedialog.askdirectory(parent=self)
        if selected:
            variable.set(selected)

    def _refresh_language(self) -> None:
        self.project.language = self.language_var.get()
        geometry = self.geometry()
        for child in self.winfo_children():
            child.destroy()
        self._build_ui()
        self.geometry(geometry)
        self.refresh_groups()

    def _sync_project(self) -> None:
        self.project.language = self.language_var.get()
        self.project.target_game_data = self.game_data_var.get().strip()
        self.project.output_mod_directory = self.output_var.get().strip()
        self.project.create_manifest_if_missing = self.manifest_var.get()
        self.project.mod_info.guid = self.guid_var.get().strip()
        self.project.mod_info.name = self.name_var.get().strip()
        self.project.mod_info.author = self.author_var.get().strip()
        self.project.mod_info.version = self.version_var.get().strip()
        self.project.mod_info.description = self.description_var.get().strip()
        self.project.mod_info.website = self.website_var.get().strip()
        try:
            self.project.packing.gap = int(self.gap_var.get())
            self.project.packing.maximum_texture_size = int(self.maximum_size_var.get())
        except ValueError as exc:
            raise AtlasBuilderError(self.tr("invalid_packing")) from exc

    def _load_to_ui(self) -> None:
        self.language_var.set(self.project.language)
        self.game_data_var.set(self.project.target_game_data)
        self.output_var.set(self.project.output_mod_directory)
        self.manifest_var.set(self.project.create_manifest_if_missing)
        self.guid_var.set(self.project.mod_info.guid)
        self.name_var.set(self.project.mod_info.name)
        self.author_var.set(self.project.mod_info.author)
        self.version_var.set(self.project.mod_info.version)
        self.description_var.set(self.project.mod_info.description)
        self.website_var.set(self.project.mod_info.website)
        self.gap_var.set(str(self.project.packing.gap))
        self.maximum_size_var.set(str(self.project.packing.maximum_texture_size))
        self._refresh_language()

    def new_project(self) -> None:
        self.project = ProjectConfig(target_game_data=DEFAULT_GAME_DATA)
        self._load_to_ui()
        self.status_var.set(self.tr("ready"))

    def open_project(self) -> None:
        path = filedialog.askopenfilename(parent=self, filetypes=(("Atlas project", "*.atlas-project.json"), ("JSON", "*.json")))
        if not path:
            return
        try:
            self.project = ProjectConfig.load(Path(path))
            self._load_to_ui()
            self.status_var.set(path)
        except Exception as exc:
            messagebox.showerror(self.tr("error"), str(exc), parent=self)

    def save_project(self) -> None:
        if self.project.project_path is None:
            self.save_project_as()
            return
        try:
            self._sync_project()
            self.project.save(self.project.project_path)
            self.status_var.set(str(self.project.project_path))
        except Exception as exc:
            messagebox.showerror(self.tr("error"), str(exc), parent=self)

    def save_project_as(self) -> None:
        path = filedialog.asksaveasfilename(parent=self, defaultextension=".atlas-project.json", filetypes=(("Atlas project", "*.atlas-project.json"),))
        if not path:
            return
        try:
            self._sync_project()
            self.project.save(Path(path))
            self.status_var.set(path)
        except Exception as exc:
            messagebox.showerror(self.tr("error"), str(exc), parent=self)

    def refresh_groups(self) -> None:
        for item in self.group_tree.get_children():
            self.group_tree.delete(item)
        for index, group in enumerate(self.project.groups):
            self.group_tree.insert(
                "",
                "end",
                iid=str(index),
                values=(group.gm_file_name, group.colour_directory, self.tr(group.mask_mode), group.source_prefix),
            )

    def add_group(self) -> None:
        dialog = GroupDialog(self)
        self.wait_window(dialog)
        if dialog.result:
            self.project.groups.append(dialog.result)
            self.refresh_groups()

    def _selected_index(self) -> int | None:
        selected = self.group_tree.selection()
        if not selected:
            messagebox.showwarning(self.tr("warning"), self.tr("select_row"), parent=self)
            return None
        return int(selected[0])

    def edit_group(self) -> None:
        index = self._selected_index()
        if index is None:
            return
        dialog = GroupDialog(self, self.project.groups[index])
        self.wait_window(dialog)
        if dialog.result:
            self.project.groups[index] = dialog.result
            self.refresh_groups()

    def remove_group(self) -> None:
        index = self._selected_index()
        if index is not None:
            del self.project.groups[index]
            self.refresh_groups()

    def _run_worker(self, operation) -> None:
        self.validate_button.configure(state="disabled")
        self.build_button.configure(state="disabled")
        self.progress.start(10)
        threading.Thread(target=self._worker, args=(operation,), daemon=True).start()

    def _worker(self, operation) -> None:
        try:
            result = operation(lambda message: self.work_queue.put(("progress", message)))
            self.work_queue.put(("success", result))
        except Exception as exc:
            self.work_queue.put(("error", (exc, traceback.format_exc())))

    def validate_project(self) -> None:
        try:
            self._sync_project()
        except Exception as exc:
            messagebox.showerror(self.tr("error"), str(exc), parent=self)
            return
        self._run_worker(lambda callback: ("validate", prepare_project(self.project, callback)))

    def build_atlases(self) -> None:
        try:
            self._sync_project()
        except Exception as exc:
            messagebox.showerror(self.tr("error"), str(exc), parent=self)
            return
        if self.project.project_path is None:
            messagebox.showwarning(self.tr("warning"), self.tr("unsaved_project"), parent=self)
            return
        existing = existing_output_groups(self.project)
        overwrite = False
        if existing:
            overwrite = messagebox.askyesno(self.tr("warning"), self.tr("overwrite", groups="\n".join(existing)), parent=self)
            if not overwrite:
                return
        self._run_worker(lambda callback: ("build", build_project(self.project, overwrite, callback)))

    def _write_report(self, text: str) -> None:
        if self.project.project_path is None:
            return
        report = self.project.project_path.with_name(self.project.project_path.stem + ".last-build.txt")
        report.write_bytes(text.replace("\r\n", "\n").replace("\r", "\n").replace("\n", "\r\n").encode("utf-8"))

    def _poll_queue(self) -> None:
        try:
            while True:
                kind, payload = self.work_queue.get_nowait()
                if kind == "progress":
                    self.status_var.set(str(payload))
                elif kind == "error":
                    exc, details = payload
                    self.validate_button.configure(state="normal")
                    self.build_button.configure(state="normal")
                    self.progress.stop()
                    self.status_var.set(str(exc))
                    self._write_report(f"{datetime.now().isoformat()} ERROR\n{details}")
                    messagebox.showerror(self.tr("error"), str(exc), parent=self)
                elif kind == "success":
                    self.validate_button.configure(state="normal")
                    self.build_button.configure(state="normal")
                    self.progress.stop()
                    operation, result = payload
                    if operation == "validate":
                        groups = result
                        frames = sum(len(group.source_frames) for group in groups)
                        masks = sum(len(group.source_frames) for group in groups if group.config.mask_mode != "none")
                        warnings = [warning for group in groups for warning in group.warnings]
                        message = self.tr("validation_ok", groups=len(groups), frames=frames, masks=masks)
                    else:
                        warnings = result.warnings
                        message = self.tr("build_ok", frames=result.colour_frames, masks=result.mask_frames, output=result.output_mod_directory)
                    if warnings:
                        message += "\n\n" + "\n".join(warnings)
                    self.status_var.set(self.tr("success"))
                    self._write_report(f"{datetime.now().isoformat()} SUCCESS\n{message}\n")
                    messagebox.showinfo(self.tr("success"), message, parent=self)
        except queue.Empty:
            pass
        self.after(100, self._poll_queue)


def run_gui() -> None:
    AtlasBuilderApp().mainloop()
