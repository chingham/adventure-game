using AdventureGame.App;
using AdventureGame.Flow;
using AdventureGame.Presentation;
using Quark.Kit;
using Quark.Kit.Ui;

namespace AdventureGame.Features.Dialogue;

/*
 * Plusieurs présentations:
 * - Speech: Les dialogues classiques, bandeau en bas, controle joueur verrouillé
 * - Bark: Les dialogues qui apparaissent au dessus de la tête du personnage, sans intéraction, controle joueur libre
 * - Notice: Des sortes de toast simples, au milieu en haut de l'écran, avec file d'attente
 * - Acquisition: Acquisition d'objets, carte centrée à l'écran avec belle présentation de l'objet qu'on vient d'avoir
 */

sealed class DialogueFeature : IGameFeature {
    public void Provide(Game game) {
        var flow = game.Find<GameFlow>()!;
        game.Provide(new Speech(flow));
    }
    public void Install(Game game) {
        var speech = game.Find<Speech>()!;
        var fonts = game.Find<Fonts>()!;
        game.Ui.Add(new ConversationPanel(speech, fonts));
        
        game.AddSystem<SpeechSystem>(QuarkPhases.Input);
    }
}