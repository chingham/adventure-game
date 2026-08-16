namespace AdventureGame.Features.Inventory;

public class Inventory {
    public InventoryItem[] Items { get; } = [
        new("Tunique", "Vêtement", "Vêtement bleu traditionnel de Twinsen. Confortable et résistant.", "Un symbole d'espoir et d'aventure."),
        new("Balle magique", "Arme", "Tu peux lancer ta Balle Magique en lui faisant faire toutes sortes de rebonds.", "Un outil magique pour résoudre des énigmes et combattre des ennemis."),
        new("Médaillon de Sendell", "Accessoire", "Tu viens d'atteindre le troisième niveau de Magie en retrouvant le Médaillon de Sendell.", "Un artefact ancien qui renforce tes pouvoirs magiques."),
    ];
}

public record InventoryItem(string Name, string Type, string Description, string? Hint); 