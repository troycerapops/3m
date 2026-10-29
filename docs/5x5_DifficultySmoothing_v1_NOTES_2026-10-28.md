# 5x5 – Difficulty smoothing v1 (2026-10-28)

## What changed
Tuned evaluation is now the ONLY evaluation, for both sides, at every difficulty. The Original/Tuned
dropdown is gone. Medium and Hard presets get deeper search.

| Tier   | Before                         | After                          |
|--------|--------------------------------|--------------------------------|
| Easy   | depth 1, TopN 4, blunder 25%/30, Random model | unchanged                |
| Medium | depth 3, TopN 2, blunder 8%/20, Greedy        | depth 4, TopN 1, no blunders, Optimal |
| Hard   | depth 5, Original eval         | depth 6                        |
| Expert | depth 8, bonus 2 / 600000      | unchanged (now also tuned Offense) |

## Files (copy into your project, overwrite)
| File in this package | Destination folder in your project |
|---|---|
| Assets/Scripts/AI/AIProfile.cs    | Assets/Scripts/AI/ |
| Assets/Scripts/AI/EvalWeights.cs  | Assets/Scripts/AI/ |
| Assets/Scripts/AI/MinimaxAgent.cs | Assets/Scripts/AI/ |

GameManager.cs and everything else: unchanged.

## Inspector – your Opponent assets keep their old saved values, so set these by hand
Removed field "Evaluation" simply disappears from the Inspector (Unity ignores the old saved value).
- Medium asset: Search Depth 4, Top N 1, Blunder Chance 0, Opponent Model Optimal
- Hard asset:   Search Depth 6, Top N 1, Blunder Chance 0, Opponent Model Optimal
- Easy asset:   no change
- Expert asset: no change (Search Depth 8, Bonus Depth Max 2, Bonus Node Limit 600000)

## Measured ladder (AI vs AI, % of games the OFFENSE side wins; n=200 per cell, Expert column/row n=60)
| Offense \ Defense | Easy | Medium | Hard | Expert |
|---|---|---|---|---|
| Easy   | 97.0 | 53.5 | 29.0 | 8.3  |
| Medium | 98.0 | 69.0 | 43.5 | 11.7 |
| Hard   | 99.0 | 71.5 | 40.5 | 10.0 |
| Expert | 98.3 | 83.3 | 58.3 | 30.0 |

Reading it: down any column = how the Offense AI tiers rank; along any row = how the Defense AI tiers rank.
Defense AI (use the Hard Offense row): 99 / 71.5 / 40.5 / 10 – evenly spaced, about 30 points a step.
Offense AI (use the Hard Defense column): 29 / 43.5 / 40.5 / 58 – Easy->Medium and Hard->Expert step up,
but Medium and Hard are indistinguishable.

## Known follow-up (not in this package)
Offense depths 4, 5 and 6 all score the same (about 44% vs Hard Defense); depth 7 scores 69%. The
Offense AI only improves at odd depths 7 and up. Options: set Hard Offense to depth 7 in its Inspector
asset (cheap), or review Expert Offense settings. Needs your call. Values are +/- 5 points.

## Git (Windows cmd, from the repo root, i.e. the folder that contains Assets)
git add .
git commit -m "AI: tuned evaluation for every tier and both sides; remove Evaluation dropdown; Medium depth 4, Hard depth 6"
git push
