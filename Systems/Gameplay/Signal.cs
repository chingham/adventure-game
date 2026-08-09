namespace AdventureGame.Systems;

// Named wire between devices, published on the ECS event channel. Sources emit a name, consumers
// filter on it; neither knows the other. Completion is reported back as "<name>.done".
readonly record struct Signal(string Name);
