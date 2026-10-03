# Draft: East Africa trivia pack (not shipped)

Status: **wired in as the "East Africa" question set on the Trivia setup, but not yet reviewed by you.** I wrote these
from general knowledge and I am confident of each answer, but please check every one before relying on them, and
replace anything you would not stand behind in front of a room. Add or swap questions for places and things your
players actually know (towns, food, music, local history).

The questions live in `backend/RandomRoom.Api/Games/Content/trivia-east-africa.json` (same shape as
`trivia-starter.json`; in the shipped file the right answer's position is shuffled so it is not always in the same
place, so the table below lists them in reading order only). To change the pack, edit that file; the setup form shows
the pack's size from `STARTER_PACKS` in `frontend/src/games/trivia/setup.ts`, so keep the two in step.

The correct answer is marked with a star in the table below.

| # | Category | Question | Options |
|---|---|---|---|
| 1 | Uganda | What is the capital of Uganda? | ★ Kampala, Entebbe, Jinja, Gulu |
| 2 | Uganda | In which year did Uganda become independent? | 1957, ★ 1962, 1966, 1971 |
| 3 | Uganda | Which bird is on Uganda's flag and coat of arms? | Marabou stork, Shoebill, ★ Grey crowned crane, African fish eagle |
| 4 | Uganda | Which Ugandan town is known as the source of the White Nile? | Masaka, ★ Jinja, Mbale, Fort Portal |
| 5 | Uganda | Which waterfalls on the Nile are inside a famous Ugandan national park? | Victoria Falls, ★ Murchison Falls, Blue Nile Falls, Kalambo Falls |
| 6 | Uganda | Which Ugandan mountains are known as the "Mountains of the Moon"? | Elgon, Moroto, ★ Rwenzori, Muhavura |
| 7 | Uganda | Bwindi Impenetrable Forest is famous for which animal? | Lions, ★ Mountain gorillas, Elephants, Hippos |
| 8 | Uganda | Which imaginary line crosses southern Uganda? | Tropic of Cancer, ★ The Equator, Prime Meridian, Tropic of Capricorn |
| 9 | Uganda | What is the currency of Uganda? | Kenyan shilling, Ugandan pound, ★ Ugandan shilling, Rwandan franc |
| 10 | Food | Matooke is made from which food? | Cassava, Sweet potato, ★ Green bananas, Maize |
| 11 | Region | What is the capital of Kenya? | Mombasa, ★ Nairobi, Kisumu, Nakuru |
| 12 | Region | What is the capital of Rwanda? | Butare, Gisenyi, ★ Kigali, Musanze |
| 13 | Region | Which city is the official capital of Tanzania? | Dar es Salaam, ★ Dodoma, Arusha, Mwanza |
| 14 | Region | Which is the highest mountain in Africa? | Mount Kenya, Mount Elgon, ★ Mount Kilimanjaro, Mount Stanley |
| 15 | Region | Which is the largest lake in Africa by area? | Lake Tanganyika, ★ Lake Victoria, Lake Malawi, Lake Albert |
| 16 | Region | Which East African country is called the "Land of a Thousand Hills"? | Uganda, Kenya, ★ Rwanda, Tanzania |
| 17 | Region | Which language is widely spoken as a common language across East Africa? | Amharic, Zulu, ★ Swahili, Hausa |
| 18 | Region | The Serengeti is famous for the yearly migration of which animal? | Zebra only, ★ Wildebeest, Elephants, Giraffes |
| 19 | Region | In which year did Kenya become independent? | 1960, 1961, ★ 1963, 1964 |
| 20 | Region | Which of these countries has the most people? | Uganda, ★ Ethiopia, Kenya, Tanzania |

Notes for the reviewer:
- #4: the Nile's source is a matter of convention; "Jinja" is the usual answer in Ugandan school and tourism material.
  Rephrase if you want to avoid the debate.
- #20: Ethiopia has by far the most people of the four (well over 100 million; Tanzania, Kenya and Uganda are each
  roughly 50 to 70 million). I worded it "of these countries" on purpose: "East Africa" can mean the East African
  Community, which does not include Ethiopia, or the wider region, which does. No figure is in the question, so it
  stays true as populations change.
- All 20 are written to be fair to Ugandan and wider East African players. Tone is family-friendly.
