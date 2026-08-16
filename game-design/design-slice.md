# Nuit sur la Citadelle — document de design de la slice

Fan game LBA2 · moteur Quark · solo dev, sans contrainte de temps
Document vivant : tout ce qui est ici est décidé, tout ce qui est en « questions ouvertes » se tranchera manette en main.

---

## 1. L'objectif

Produire **une slice verticale de 10 à 15 minutes**, jouable de bout en bout, du plan titre à l'écran de fin, **sans une seule coupure** : pas d'écran de chargement, pas de fondu au noir, pas de transition de scène visible.

La slice doit prouver quatre choses :

1. que le moteur tient un vrai jeu (rendu, animation, audio, physique, UI) ;
2. que le concept de caméra fonctionne — follow en extérieur, isométrique en intérieur, transitions continues ;
3. qu'une quête complète peut être écrite en data (séquences, flags, dialogues) sans code spécifique ;
4. que la direction artistique tient — nuit, pluie, lumières chaudes, style low-poly stylisé.

Ce n'est **pas** une démo du jeu final ni un remake fidèle d'une zone de LBA2. C'est une tranche autonome qui emprunte l'univers, l'esprit et l'ambiance de LBA, avec une quête originale.

**Non-objectifs de la slice :** le combat, la balle magique comme mécanique, les choix de dialogue, plusieurs quartiers, le cycle jour/nuit, la mort du joueur.

---

## 2. Les partis pris

| Sujet | Décision |
|---|---|
| Caméra extérieure | Third person follow (façon Kena), pas d'angles fixes. Pas de rotation libre en iso. |
| Caméra intérieure | Isométrique, cadrée par volume (Space), transition continue par la porte. |
| Direction artistique | Réinterprétation libre, low-poly stylisé à palette, esprit Kena — pas une copie du look 1997. |
| Contrôles | Modernes : attaque, saut, bascule discret, bascule marche/course. Manette et clavier. |
| Ambiance | Nuit, orage, pluie continue. Alibi narratif *et* budgétaire : tout est fermé, la visibilité est courte. |
| Modularité | Pas de grille de tiles. Des **mesures communes** (hauteur de porte, d'étage, de marche, épaisseur de mur), et du terrain organique. |
| Continuité | Aucune coupure. Le plan titre est un état du monde, pas un écran séparé. |
| Ton | Personne n'est méchant. Les obstacles sont des circonstances, pas des antagonistes. |

---

## 3. Le pitch

Twinsen se réveille en pleine nuit d'orage. Il veut récupérer un objet qu'il avait confié à l'apothicaire du bourg. La boutique est ouverte — le vieux travaille tard — mais l'objet est rangé au fond, dans une arrière-boutique dont l'ampoule a lâché : on n'y voit rien.

Sur le quai, le gardien du phare regarde son phare éteint : sa mèche est noyée par la pluie. L'apothicaire a de quoi rallumer. Le gardien rallume le phare. Le faisceau balaie l'arrière-boutique toutes les quelques secondes, et Twinsen peut enfin s'y repérer.

**Le twist :** le phare éteint est visible dès la première sortie de la maison, révélé par un éclair, dix minutes avant qu'on en ait besoin. Rien n'est verrouillé : un joueur têtu peut traverser l'arrière-boutique à l'aveugle et finir la slice sans jamais parler au gardien. C'est voulu.

---

## 4. Le déroulé, plan par plan

**Plan titre.** Caméra en gros plan sur le **médaillon de Sendell**, posé sur la table de chevet près de la fenêtre battue par la pluie. Fort DOF : l'arrière-plan n'est que du bokeh. Menu à gauche.

**Nouvelle partie.** Le menu fond, la caméra recule et pivote à travers la pièce, se dépose dans le rig isométrique de la maison. Twinsen s'étire à côté du lit défait — on comprend qu'il se réveille, sans animation de sortie de lit. Contrôle rendu au joueur. *(« Continuer » rejoue le même mouvement vers la position sauvegardée.)*

**1 — La maison.** Twinsen dit ce qu'il cherche. Le joueur se balade, apprend les contrôles, fouille : décor, et **le reçu de l'apothicaire**. Le dialogue de réveil ne dit jamais où aller — c'est le joueur qui déduit.

**2 — La sortie.** Pluie, nuit, clair de lune. Un éclair révèle brièvement **le phare éteint** au loin. Aucun soulignement, aucun dialogue.

**3 — La descente.** Le sentier contrôle le champ de vision. En bas, la place portuaire : maisons closes, silhouettes derrière les rideaux, et **la vitrine allumée de l'apothicaire** — le seul appel visuel.

**4 — L'apothicaire.** Dialogue banal, chaleureux. Le vieux fait sa ronde : derrière le comptoir, puis il va ranger quelque chose dans un coin. Si le joueur approche du fond pendant qu'il est au comptoir, il le prévient gentiment : *« n'y va pas, l'ampoule a lâché, tu vas te casser une jambe dans mon bazar »*. Ce n'est pas une interdiction, c'est **l'information "il faut de la lumière"**. Quand il s'éloigne, le passage est libre — et dès que le joueur est passé, la boucle du PNJ s'arrête (il ne fait pas demi-tour).

**5 — L'arrière-boutique.** Noir, labyrinthique, quelques mètres de visibilité. Pas de prompt d'interaction sans lumière. Plusieurs caisses identiques : fouiller à l'aveugle donne des « ce n'est pas ça » à répétition. L'apothicaire relance depuis la boutique : *« je t'avais dit qu'on n'y voit rien ! »*. Un joueur têtu peut aller au bout — **fin de slice pour lui**.

**6 — Le quai.** Le gardien du phare, sous un auvent, regarde son phare éteint. Sa mèche est noyée. Il ne demande rien d'extraordinaire : de quoi rallumer.

**7 — Retour à la boutique.** Nouveau dialogue (conditionnel) : l'apothicaire donne ce qu'il faut, en précisant que ça ne servira à rien par ce temps. Il a tort.

**8 — Le gardien part.** Il dit qu'il y va et sort du champ. **Le phare s'allume sur un trigger** (l'entrée de la pharmacie), jamais sur un timer : le joueur ne peut pas assister à l'incohérence, et il découvre le faisceau en ressortant.

**9 — Le final.** Le faisceau balaie l'arrière-boutique (≈ 3 s de clarté toutes les 5 s) plus une ambiante faible. Le joueur se repère, avance, trouve l'objet. Éclair, plan sur la place, fin de slice.

---

## 5. Lieux, personnages, objets

**Lieux :** maison de Twinsen (intérieur iso, point de départ, source de l'indice) · sentier · place portuaire (extérieur follow, 3 façades closes non visitables) · apothicaire (intérieur iso) · arrière-boutique (intérieur iso, sombre) · quai · **phare (vista, non visitable, horloge du monde)**.

**PNJ :** l'**apothicaire-bricoleur**, vieux, chaleureux, range mal, travaille la nuit — il ne se souvient pas avoir rangé l'objet au fond, donc il ne peut pas te le donner. Le **gardien du phare**, sur le quai, sa mèche noyée. Zoé est absente (chez sa mère, à l'autre bout de l'île) : un PNJ de moins, et pas de trope de l'homme qui dépend de sa femme.

**Objets :** le médaillon de Sendell (hero prop du plan titre, budget macro, sert aussi au showroom d'inventaire) · le reçu (indice, dans la maison) · l'objet confié à réparer — un petit mécanisme hérité du grand-père, valeur sentimentale, aucune explication nécessaire · de quoi rallumer (mèche, briquet, combustible).

---

## 6. Systèmes que la slice implique

Au-delà du plan de production existant, ce script demande :

- **Dialogues conditionnels** — l'apothicaire dit autre chose une fois le gardien rencontré. Conditions sur flags, état par PNJ. *Seul vrai ajout non prévu ; inévitable pour la suite du jeu de toute façon.*
- **Gating des interactions par la lumière** — pas de prompt dans le noir. Version saine : déclaré par le Space (« ici c'est noir tant que le flag phare n'est pas levé »), pas d'échantillonnage de luminance.
- **Routine de PNJ avec états et interruption** — boucle de waypoints qui s'arrête définitivement quand le joueur passe derrière.
- **Blocage doux par dialogue** — trigger + séquence, gratuit.
- **Flags qui modifient le monde** — le phare allumé change l'éclairage extérieur *et* l'arrière-boutique.
- **Lumière animée** — intensité pulsée du faisceau. Une dizaine de lignes.
- **Rig cinématique** — pour le plan titre et le dézoom.

---

## 7. Faire vivre l'île sans 50 assets

La nuit d'orage est l'alibi : tout est fermé parce qu'il est tard et qu'il pleut. Portes closes, fenêtres allumées avec une silhouette derrière un rideau (un quad + une texture), linge oublié qui claque, barques amarrées qui tanguent, un chat sous un auvent.

**L'audio fait 70 % du travail** : rires étouffés derrière une porte, vaisselle, une radio lointaine, un volet qui bat. Trois façades non visitables suffisent si elles sonnent habitées. Le fait que seuls deux lieux soient ouverts devient une information sur le monde, pas une limite technique.

---

## 8. Règles de conception tenues jusqu'ici

1. **La porte est verrouillée par de la connaissance, pas par un objet.** Un savoir ne se brute-force pas.
2. **L'énigme ne repose jamais sur un geste sans incitation.** Le joueur doit être récompensé pour une curiosité qu'il avait déjà, pas pour un tâtonnement absurde.
3. **Rien n'est bloqué, tout est dissuadé.** Le joueur têtu gagne. C'est l'esprit Outer Wilds.
4. **L'indice est montré avant d'être utile, et jamais souligné.**
5. **Aucun antagoniste.** Les obstacles sont des circonstances.
6. **Le joueur n'assiste jamais à une incohérence** — pas de trajet de PNJ impossible à montrer : on montre le résultat, déclenché par un trigger.

---

## 9. Questions ouvertes — à trancher en jouant, pas sur le papier

- Nombre et disposition des fausses caisses, taille du labyrinthe : réglage de la difficulté du bourrinage à l'aveugle. **Tester tôt** : si la moitié des joueurs traverse en 30 s, l'acte II tombe.
- Période exacte du faisceau (viser 3 s de clarté / 5 s de cycle) et niveau de l'ambiante faible.
- Quel objet exact est activable derrière le comptoir, et comment il se lit sans être évident.
- Phrasé précis des dialogues.
- Durée de la ronde de l'apothicaire.
- Rythme et fréquence des éclairs en extérieur.

---

## 10. Prochaine étape

**M0 — la fusion.** Un exécutable, un format de scène, une seule autorité de contrôle joueur. Le design est assez posé pour ne plus bloquer la production ; tout le reste se règle manette en main.
