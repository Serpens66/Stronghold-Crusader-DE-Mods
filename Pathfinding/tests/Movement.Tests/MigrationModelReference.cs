extern alias PathfindingModel;
// Resolve the actual model schema without loading a game/plugin runtime in the test process.
global using PathfindingViewModel = PathfindingModel::Pathfinding.PathfindingViewModel;
global using PathfindingPlugin = PathfindingModel::Pathfinding.PathfindingPlugin;
