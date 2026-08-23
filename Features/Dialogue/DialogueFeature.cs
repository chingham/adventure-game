using AdventureGame.App;
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
    public void Provide(IServiceRegistry services) {
        services.Add<Speech>();
    }

    public void Install(Game game) {
        game.AddSystem<SpeechSystem>(QuarkPhases.Input, Order.Speech);
        game.AddUi<ConversationPanel>();
    }
}
