# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

"Bluff" is a multiplayer card game built in Unity using **Photon Fusion** for networking. The game supports 2–6 players who take turns placing bets (declaring a rank for cards they play, possibly bluffing), with opponents able to challenge the bet.

## Development

This is a Unity project — there are no CLI build or test commands. Open and run the project through the **Unity Editor**. The Photon Fusion SDK is installed as a local package (not in `Packages/manifest.json`). A valid Fusion App ID must be set in `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`.

## Architecture

### Core Game Logic (`Bluff.Core` namespace — no Unity dependencies)

- **`Card`** / **`Suit`** / **`Rank`** — immutable card model
- **`Deck`** — 52-card or 36-card (short) deck; short deck (6 and above) used for ≤3 players
- **`Player`** — player identity + hand management; `Id` is always the string of the player's integer index (`"0"`, `"1"`, etc.)
- **`GameState`** — all mutable game state: players, pile, discard, current turn, last bet
- **`GameRules`** — static validation (`CanPlaceBet`, `CanChallenge`) and resolution (`ResolveBelieve`, `ResolveBluff`)

### Game Managers

There are two parallel game managers — the UI checks `NetworkedGameManager.Instance != null` to decide which path to take:

- **`GameManager`** (offline, `MonoBehaviour` singleton) — wraps `GameState` directly, for local/testing use
- **`NetworkedGameManager`** (online, `NetworkBehaviour` singleton) — Photon Fusion host/client topology

### Networking Pattern (`NetworkedGameManager`)

- The **host** holds `StateAuthority` and owns the canonical `_localState`
- `[Networked]` properties sync only scalar values (current player index, pile count, etc.)
- Full card data is sent via **RPCs**, not networked properties
- Each client only receives their own real cards; opponents' cards are stored as placeholder `Ace of Spades`
- Player identity: `PlayerRef` → integer index via `_playerIndexMap`; index is assigned by host via `RPC_AssignPlayerIndex`
- Flow: client action → `RPC_PlaceBet`/`RPC_Believe`/`RPC_Bluff` (to StateAuthority) → host resolves → `RPC_BetPlaced`/`RPC_BelieveResolved`/`RPC_BluffResolved` (broadcast to all) → clients update local state + refresh UI

### Network Connection (`NetworkManager`)

Implements `INetworkRunnerCallbacks`. On `OnPlayerJoined`, the host spawns the `NetworkedGameManager` prefab, then every player waits for it to appear before registering via `LocalPlayerJoined`.

### UI (all built procedurally in C# — no scene prefabs)

All UI GameObjects and components are created in `Start()` / `BuildUI()` methods:

- **`LobbyUI`** — full-screen lobby: player name + room code inputs, Create/Join buttons
- **`UIManager`** — main game UI with three panels:
  - Top (0.78–1.0): opponents' card counts
  - Middle (0.38–0.78): current turn status, active bet info, pile/discard counts
  - Bottom (0–0.38): local player's hand (fan layout) + Believe / Bluff! / Bet buttons
- **`GuessingScreenUI`** — full-screen overlay (sortingOrder 120) shown when a player picks a card to reveal (Believe/Bluff); shows a card-flip animation and result. Created programmatically by UIManager if not present.
- **`RankPickerUI`** — modal for choosing declared rank when opening a new bet
- **`CardView`** — individual card component; fires `OnCardClicked` event; `SetSelected(bool)` raises the card visually

### Game Rules Summary

- On your turn with an active bet: you can **Believe** (pick a card, check if rank matches → correct = pile to discard, wrong = you take pile), **Bluff** (pick a card → caught lying = liar takes pile, honest = you take pile), or **Re-bet** (play 1–4 cards with the same declared rank)
- On your turn with no active bet: you must **Bet** (select 1–4 cards, pick any rank to declare)
- A player with no cards but an active bet must Believe or Bluff
- **Loser**: the last player who still has cards when all others have emptied their hands
