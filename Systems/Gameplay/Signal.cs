using Quark.Ecs;

namespace AdventureGame.Systems;

// Named wire between devices, published on the ECS event channel. Sources emit a name, consumers
// filter on it; neither knows the other. Completion is reported back as "<name>.done". The object
// that sent it rides along for whatever wants to know what was acted on - a sequence turning the
// player toward it, say - and consumers that only care about the name ignore it.
readonly record struct Signal(string Name, Entity Source = default);
