using Quark.Kit;
using Quark.Kit.Assets;
using Quark.Kit.Ui;

namespace AdventureGame.Ui;

/// <summary>
/// The faces the demo is set in, loaded once through the asset library and handed to the panels. Every panel
/// asking for its own <c>UiFont.FromFile</c> read, baked and atlased the same face several times over.
/// </summary>
sealed record Fonts(UiFont Regular, UiFont Medium, UiFont Wide) {
    public static Fonts Build(Game game) => new(
        game.Assets.Font("Data/Fonts/Fredoka_SemiCondensed-Regular.ttf"),
        game.Assets.Font("Data/Fonts/Fredoka_SemiCondensed-Medium.ttf"),
        game.Assets.Font("Data/Fonts/Fredoka-Medium.ttf"));
}
