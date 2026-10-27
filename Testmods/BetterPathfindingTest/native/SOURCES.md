# Native area-navigation prototype

The backend is original code. No third-party implementation was copied or linked.

- Harabor, Botea, Kilby: [Path Symmetries in Undirected Uniform-Cost Grids](https://pathfinding.ai/pdf/harabor-botea-kilby-sara11.pdf). Rectangle interiors, boundary fans and compact reconstruction motivate the reduction. This backend uses equal-cost eight-direction **Chebyshev** steps, not the paper's octile weights. Only cells whose original outgoing edges have a uniform class can be merged. Directional/heterogeneous exceptions remain explicit. Differential tests, not the paper alone, establish this adaptation's coverage.
- Emerson: [Crowd Pathfinding and Steering Using Flow Field Tiles](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter23_Crowd_Pathfinding_and_Steering_Using_Flow_Field_Tiles.pdf). Shared goal fields motivate the four-entry bounded field cache and batch requests. This prototype outputs discrete private paths; it does not steer game units.
- Uras, Koenig, Hernandez: [Subgoal Graphs for Optimal Pathfinding in Eight-Neighbor Grids](https://ojs.aaai.org/index.php/ICAPS/article/view/13568). Motivation for concentrating work at transitions; no claim to implement that paper's subgoal algorithm.

Uniform rectangle interiors have a single ground/moat and structure class. A shortest interior walk has `max(abs(dx),abs(dy))` steps. Replacing a longer such walk reduces every positive profile cost and the step count. The wet-visit state is retained, including positive two-step wet excursions. Nonuniform or directed regions retain the original directed edges. Terminal overrides and scalar optima violating resource bounds use a separately implemented native multi-label A* on the original snapshot. They never invoke the former hierarchy or the production solver.

The C ABI accepts only private snapshot arrays. Exceptions cannot cross the ABI. Calls are worker-only, use a counted allocator, and expose bounded stepping and incomplete results. There are no imports from the game, no native hooks and no command/path-buffer writes. Normal release packages do not include this diagnostic backend.
