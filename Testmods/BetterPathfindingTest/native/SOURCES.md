# Native area-navigation prototype

The backend is original code. No third-party implementation was copied or linked.

- Harabor, Botea, Kilby: [Path Symmetries in Undirected Uniform-Cost Grids](https://pathfinding.ai/pdf/harabor-botea-kilby-sara11.pdf). Rectangle interiors, boundary fans and compact reconstruction motivate the reduction. This backend uses equal-cost eight-direction **Chebyshev** steps, not the paper's octile weights. Only cells whose original outgoing edges have a uniform class can be merged. Directional/heterogeneous exceptions remain explicit. Differential tests, not the paper alone, establish this adaptation's coverage.
- Emerson: [Crowd Pathfinding and Steering Using Flow Field Tiles](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter23_Crowd_Pathfinding_and_Steering_Using_Flow_Field_Tiles.pdf). Shared goal fields motivate the four-entry bounded field cache and batch requests. This prototype outputs discrete private paths; it does not steer game units.
- Uras, Koenig, Hernandez: [Subgoal Graphs for Optimal Pathfinding in Eight-Neighbor Grids](https://ojs.aaai.org/index.php/ICAPS/article/view/13568). Motivation for concentrating work at transitions; no claim to implement that paper's subgoal algorithm.

Distinct/small target groups use exact forward A* on the reduced graph, with a
generation-stamped reusable workspace. A consistent Chebyshev lower bound uses the
minimum edge-class cost; it does not assume moat is always slower. Larger matching
cohorts integrate one reverse goal field. Existing cached fields remain usable by
single requests. Local fields are seeded from those exact global boundary values,
not from independent searches at every boundary point. Cohorts are scheduled by
full goal/profile context and original request identities bind reordered outputs.

For the equal-cost diagonal adaptation, crossing a uniform rectangle costs the
Chebyshev distance. Boundary walks and diagonal/opposite-side fans preserve that
distance: offsets exceeding a fan's diagonal extent are covered by a boundary walk
with the same maximum-coordinate displacement. Queries with interior endpoints
insert exact virtual connections. All macro witnesses remain inside the uniform
rectangle. Positive two-step wet excursions preserve required visits when start
and endpoint coincide. Differential tests exhaust small rectangle endpoint pairs
and cover weighted seams, filters, cycles, directed exceptions and shared local
fields. This is evidence for the implemented subset, not a universal proof for
arbitrary future movement rules.

Uniform rectangle interiors have a single ground/moat and structure class. A shortest interior walk has `max(abs(dx),abs(dy))` steps. Replacing a longer such walk reduces every positive profile cost and the step count. The wet-visit state is retained, including positive two-step wet excursions. Nonuniform or directed regions retain the original directed edges. Terminal overrides and scalar optima violating resource bounds use a separately implemented native multi-label A* on the original snapshot. They never invoke the former hierarchy or the production solver.

The C ABI accepts only private snapshot arrays. Exceptions cannot cross the ABI. Calls are worker-only, use a counted allocator, and expose bounded stepping and incomplete results. There are no imports from the game, no native hooks and no command/path-buffer writes. Normal release packages do not include this diagnostic backend.

## Subsequent offline CCH decision gate, 2026-10-27

`CchProbe.cpp` is a separate original implementation of the first decision gate,
not a replacement already loaded by the installed mod. It builds geometric nested-
dissection ranks, an elimination tree and symbolic fill for the full eight-neighbor
coordinate potential graph. Directed basic customization introduces blocked arcs
as infinity; finite ground arcs have unit cost. Shortcut witnesses reconstruct
original directed steps. It uses the principles in Dibbelt/Strasser/Wagner,
[Customizable Contraction Hierarchies](https://ben-strasser.net/paper/customizable_contraction_hierarchies_arxiv_preprint.pdf).
No third-party code or library is copied or linked.

The tested variants include full elimination-tree queries and a subsequent variant
with cached target search data, reusable exact source labels, request identity after
cohort sorting, and shared reverse ground fields. Compact output reconstructs only
the first eight directions; full output is measured separately. Direct geometric
walks are accepted only after every original edge is verified and their length
reaches the Chebyshev lower bound. An open-grid direct hit is not a CCH speedup.

The private probe ABI is separate from the runtime ABI: `0x02001020`, pair 16 bytes,
result 32 bytes and 16 Int64 statistics. `Build-CchProbe.ps1` builds only
`.local/cch/CchProbe.dll`. It is not copied to BepInEx. The CCH step interface requests
2 ms work packets, but measured larger packets remain an explicit limitation.
Metric replacement currently performs full basic customization while retaining
symbolic topology; partial dependency propagation is not implemented in this gate.

The final captured-map measurements fail the reaction target even with sharing.
See [RESULTS.md](../RESULTS.md) and the immutable per-run compiler, binary, host,
input and decision records. The negative result concerns this full-potential-grid,
geometric-order implementation; it does not refute every CCH ordering, metric-
specific pruning method or routing architecture. It does not authorize silently
replacing the current runtime solver or claiming a resident public service exists.
