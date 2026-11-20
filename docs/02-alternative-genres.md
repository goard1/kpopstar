# 대안 장르 리서치 v0.1 (머지·K-팝 제외)

> 작성일: 2026-11-20 · 전제: **Unity**, 1인(+Claude) 개발, Google Play 우선 출시, 광고비 거의 없음

---

## 0. 결론 먼저

| 순위 | 후보 | 한 줄 평가 |
|---|---|---|
| **1** | **로그라이크 빌더 (Balatro류) — 새로운 소재로** | 1인 개발 성공 사례가 가장 많음. 아트 부담 적고 시스템 설계 중심이라 Claude와 같이 코드 짜기 좋음. 광고비 없이 입소문으로 팔림. Unity로 Steam 동시 출시 가능 |
| 2 | 하이브리드 캐주얼 퍼즐 (Sort/Block/Screw류) + 퍼블리셔 | 시장은 가장 뜨겁지만 공급 과잉. 혼자 마케팅은 불가능하니 **퍼블리셔 테스트 루트** 전제 |
| 3 | 서바이버라이크 액션 로그라이트 | 시장 최대급(Habby 누적 $2B)이지만 대형사 메타 경쟁 + 빠른 수명 감소 |
| 4 | 아이들/타이쿤 | 만들기 쉽고 광고 수익형. 대신 수익 상한이 낮고 차별화 어려움 |
| 5 | 코지 라이프심 | 트렌드는 맞지만 콘텐츠 양이 1인 개발 규모를 넘음 |

---

## 1. 시장 신호 요약 (2026)

- **전체:** H1 2026 모바일 인앱 매출 약 $39.8B(-2%), 다운로드 -12% → 신규 유저 확보는 어렵고, 유저당 수익이 중요.
- **하이브리드 캐주얼:** H1 2026 약 $2.4B(+23%), 유일하게 성장하는 세그먼트.
- **하이브리드 퍼즐 (Sort/Screw/Block):** H1 2026 IAP 약 $600M. 히트작 Pixel Flow는 1년도 안 돼 $125M. **그러나 블록 퍼즐만 반기에 2,000개 넘게 출시(+89%)**, 신작 120개 중 월 $100K를 넘긴 게임은 0~1개.
- **로그라이트:** Habby(Survivor.io, Archero) 누적 $2B 돌파, Archero 2는 2025년 $200M 이상. 반면 Capybara Go는 빠르게 하락 → 정점이 짧음.
- **덱빌더/로그라이크:** Balatro가 TGA 최우수 인디·데뷔 인디·**모바일 게임상** 동시 수상. 모바일로 이식되는 Balatro류가 계속 늘어남.
- **Google Play 변화 (유료 게임에 유리):**
  - 2026.3 **게임 트라이얼** — 유료 게임 정식판 무료 체험 후 구매, 진행도 유지
  - **한 번 구매로 모바일+PC 플레이**, 위시리스트 할인 알림, PC 섹션 신설
  - 2026.10 AI 기반 앱 발견 기능, **품질 우수 앱 수수료 인하 프로그램**
- **인디 현실:** 유료 광고 없이 자연 유입만으로 안정기에 **하루 $3~30** 수준이라는 추정이 있음 → "히트 1개"보다 "작은 게임 여러 개(카탈로그)" 전략이 흔함.

---

## 2. 후보별 상세

### 후보 1. 로그라이크 빌더 (Balatro / Luck Be a Landlord / Vampire Survivors류) ⭐ 추천

**무엇:** 짧은 판(15~30분) 동안 카드·주사위·심볼 등을 조합해 점수를 폭발시키는 게임. 판마다 랜덤 조합 → 무한 반복.

**왜 1인 개발자에게 맞나**
- **실제 1인 개발 성공 사례가 이 장르에 몰려 있음:** Balatro(1인), Luck Be a Landlord(1인), Vampire Survivors(1인으로 시작).
- **아트가 단순해도 됨** (카드·아이콘·UI 중심). 재미는 조합 설계에서 나옴.
- **코드 중심** → 효과 시스템·밸런스 시뮬레이션은 Claude가 많이 도와줄 수 있는 영역.
- **광고비 없이 팔림:** 스트리머·유튜버가 "사기 조합" 영상을 자발적으로 만듦.
- **Unity 1개 코드로 Android + Steam(PC) 동시 출시** → 수익 채널 2배. Google Play의 PC 섹션·크로스 구매도 활용 가능.

**수익 모델 (택1)**
| 모델 | 방식 | 장단점 |
|---|---|---|
| A. 무료 + 정식판 해금 | 1~3판/덱 1개 무료 → 전체 해금 ₩5,900~9,900 | 다운로드 장벽 낮음. 광고 없음 → 평판 좋음 |
| B. 유료 + 게임 트라이얼 | ₩6,500~12,000 유료, Google Play 체험 기능 활용 | 정직한 모델, 리뷰 좋음. 다운로드 수는 적음 |
| C. 무료 + 보상형 광고 + 광고제거 | 이어하기·리롤에 광고 | 수익은 생기지만 장르 팬이 싫어함 (비추천) |

**차별화 포인트 = 소재.** 포커·슬롯은 이미 포화. 아직 덜 쓰인 소재 예시 (출시 전 경쟁작 검색 필수):
- **화투 로그라이크** — 한국적 소재, 해외엔 신선함 (같은 아이디어 기존작 유무 확인 필요)
- **요리 레시피 빌더** — 재료 카드를 조합해 손님 점수 내기
- **낚시 덱빌더** — 미끼/릴/장소 조합, 희귀 물고기 수집
- **타자/단어 빌더** — 글자 타일 조합 (Beyond Words 같은 사례가 있다는 보도 있음)

**리스크:** 설계 난이도가 높음(재미가 조합 밸런스에 달려 있음) / 경쟁작 품질 기준이 높음 / "Balatro 짝퉁" 소리 들으면 끝 → **메커닉은 참고하되 규칙·소재·UI는 독자적으로**.

**개발 기간 (예상):** 프로토타입 2주 → 데모 6~8주 → 출시 4~6개월.

---

### 후보 2. 하이브리드 캐주얼 퍼즐 (Sort / Block / Screw / Hole류)

**무엇:** Color Block Jam, Screw Jam, Pixel Flow, All in Hole 같은 "한 손으로 하는 퍼즐 + 레벨 진행 + 실패 시 이어하기 상품".

**장점**
- 장르 매출 성장 가장 큼, 만들기 쉬움 (Unity 2D/간단한 3D).
- 수익 구조가 검증됨: **실패 직전 "이어하기" 상품(fail offer)** + 보상형 광고 + 광고 제거.

**단점 (치명적)**
- **혼자서는 유저를 못 모음.** 이 장르는 유료 광고(UA)로 키우는 장르라, 광고비 없으면 아무도 못 봄.
- 공급 과잉 (블록 퍼즐 반기 2,000개+). 성공률 1/120 이하.

**현실적 전략: 퍼블리셔 루트**
- Voodoo, Homa, CrazyLabs 등 하이브리드 캐주얼 퍼블리셔는 **개발자 프로토타입을 받아 광고 테스트(CPI 테스트)를 대신 돌려줌**. 지표 좋으면 계약 → 퍼블리셔가 마케팅비 투자, 수익 분배.
- 전략: **2~3주에 프로토타입 1개씩** 만들어 여러 개 테스트. (각 퍼블리셔 조건은 접촉 시점에 확인)
- 장르 믹스 예시: "Sort + 정원 가꾸기 메타", "Hole + 수집 도감", "Block + 단어 퍼즐"(Voodoo WordFit 사례).

---

### 후보 3. 서바이버라이크 / 액션 로그라이트

**무엇:** Survivor.io, Archero류. 자동 공격 + 레벨업 스킬 선택 + 장비 성장.

- **장점:** 시장 크고 검증됨(Habby 누적 $2B, Survivor.io 2026년에도 성장). 코어는 1인도 만들 수 있음.
- **단점:** 돈 버는 부분은 코어가 아니라 **장비·가챠·이벤트 등 거대한 메타 시스템** → 1인이 운영하기 벅참. Capybara Go처럼 정점이 짧음. 대형사와 정면 경쟁.
- **틈새 공략:** 메타를 줄이고 "프리미엄 서바이버라이크"(Vampire Survivors처럼 저가 일시불)로 가면 후보 1과 비슷한 전략이 됨.

---

### 후보 4. 아이들 / 타이쿤

**무엇:** Idle Miner Tycoon, Egg Inc., AdVenture Capitalist류. 꺼놔도 돈이 쌓이고, 업그레이드 반복.

- **장점:** Unity로 가장 쉽게 만들 수 있음. 수치·밸런스 중심이라 아트 부담 적음. 매출의 60~70%가 광고(보상형과 궁합 좋음).
- **단점:** 차별화가 어렵고, 광고 수익형이라 **DAU가 곧 매출** → 결국 유입 문제로 돌아옴. 수익 상한 낮음.
- **용도:** "첫 게임으로 출시 경험 쌓기"에 적합 (출시·광고·결제·스토어 운영 학습용). 2~3개월이면 출시 가능.

---

### 후보 5. 코지 라이프심

- 트렌드는 맞음 (Heartopia 깜짝 히트, 중국 대형사들이 라이프심으로 전환 중).
- 하지만 **콘텐츠 양(맵·아이템·NPC·이벤트)이 1인 개발 규모를 크게 넘음** → 비추천. 단, 후보 1·4에 "코지 아트 스타일"을 입히는 건 좋은 선택(여성 유저 대상 기회로 언급됨).

---

## 3. 비교 매트릭스 (★ 많을수록 유리)

| 항목 | 1. 로그라이크 빌더 | 2. 하이브리드 퍼즐 | 3. 서바이버라이크 | 4. 아이들/타이쿤 | 5. 코지 심 |
|---|---|---|---|---|---|
| 시장 성장성 | ★★★ | ★★★ | ★★☆ | ★★☆ | ★★☆ |
| 1인 개발 난이도 (쉬울수록 ★) | ★★☆ | ★★★ | ★★☆ | ★★★ | ★☆☆ |
| 광고비 없이 성장 가능성 | ★★★ | ★☆☆ | ★★☆ | ★☆☆ | ★★☆ |
| 경쟁 강도 (낮을수록 ★) | ★★☆ | ★☆☆ | ★☆☆ | ★★☆ | ★★☆ |
| 수익 상한 | ★★★ | ★★★ | ★★★ | ★☆☆ | ★★☆ |
| 출시 후 운영 부담 (적을수록 ★) | ★★★ | ★★☆ | ★☆☆ | ★★☆ | ★☆☆ |
| PC(Steam) 확장성 | ★★★ | ★☆☆ | ★★☆ | ★★☆ | ★★★ |
| **합계** | **19** | **14** | **13** | **13** | **13** |

---

## 4. 추천 로드맵

**메인: 후보 1 (로그라이크 빌더)**
1. **1주차:** 소재 3개(예: 화투 / 요리 / 낚시) 각각 종이·스프레드시트 프로토타입 → 가장 재미있는 1개 선택. 소재별 기존작 조사.
2. **2~3주차:** Unity 그레이박스 프로토타입 (카드 20장, 효과 15개, 1판 완주 가능). 지인 5~10명 테스트.
3. **4~10주차:** 데모 (덱 2~3개, 보스 3개, 해금 시스템). 숏폼·Reddit에 개발일지 연재 시작, Steam 페이지 위시리스트 수집.
4. **8주차~:** Google Play 비공개 테스트 (12명×14일) 병행.
5. **4~6개월:** Android 정식 출시(무료+정식판 해금) → 반응 좋으면 Steam 출시.

**선택: 첫 출시 경험이 먼저 필요하면** 후보 4(아이들)를 2개월 안에 작게 내서 출시·광고·결제 파이프라인을 먼저 익히고 메인 게임으로 넘어가는 방법도 있음.

---

## 참고 자료
- [AppMagic — Casual Games Report H1 2026](https://appmagic.rocks/research/casual-report-H12026/?hl=en)
- [GameDev Reports — AppMagic Mobile Casual Games in H1 2026](https://gamedevreports.substack.com/p/appmagic-mobile-casual-games-in-h1)
- [Gamigion — Mobile Casual Games Report H1 2026 Analysis](https://www.gamigion.com/mobile-casual-games-report-h1-2026-analysis/)
- [AppMagic — New Puzzle Order: Sort, Block, Screw](https://appmagic.rocks/blog/new-puzzle-order-sort-block-screw/?hl=en)
- [AppMagic — Hybridcasual Puzzles: Turning Failure into Revenue](https://appmagic.rocks/blog/fail-offers)
- [AppMagic — Top 10 Hybridcasual Games Q2 2025](https://appmagic.rocks/blog/q2hybridcasual-2025)
- [Carlos Pereira — Voodoo, Tripledot & Lion: Hybridcasual Puzzle Games (2026)](https://carlospereiragame.substack.com/p/lions-ronaldinho-gaucho-3-voodoo)
- [PocketGamer.biz — Top 50 Mobile Game Makers of 2026](https://www.pocketgamer.biz/the-top-50-mobile-game-makers-of-2026/)
- [Playio — 2026 Game Genres List](https://blog.playio.co/mobile-game-genres-list)
- [Cinevva — Casual Games Trends 2026](https://app.cinevva.com/guides/casual-games-trends-2026)
- [Naavik — Why China's gaming giants are pivoting to lifestyle sims](https://naavik.co/digest/beyond-gacha-why-chinas-gaming-giants-are-pivoting-to-lifestyle-sims/)
- [GameFirms — Idle vs Hybridcasual 2026](https://gamefirms.com/blog/mobile-games/idle-games-vs-hybridcasual-games/)
- [InvestGame — Casual Mobile Games Marketing & Monetization Report](https://investgame.net/news/pdf/casual-mobile-games-a-marketing-and-monetization-report/)
- [Choost Games — Mobile games like Balatro](https://choostgames.com/blog/mobile-games-like-balatro/) · [Pocket Tactics — Best mobile card games 2026](https://www.pockettactics.com/best-mobile-card-games) · [Wikipedia — Luck Be a Landlord](https://en.wikipedia.org/wiki/Luck_Be_a_Landlord)
- [Google 블로그 — 구글플레이 유료 게임 업데이트 (게임 트라이얼)](https://blog.google/intl/ko-kr/products/android-play-hardware/google-play-paid-games-updates-kr/)
- [Google 블로그 — 2026.10 앱 발견·참여 도구](https://blog.google/intl/ko-kr/products/android-play-hardware/2026-10-new-tools-for-discovery-and-engagement-kr/)
- [PocketGamer.biz — Google Play Game Trials](https://www.pocketgamer.biz/google-play-rolls-out-game-trials-so-players-can-try-full-paid-games-before-purchasing)
- [Medium — How Solo Unity Developers Scale Mobile Revenue](https://medium.com/@unitysourcecode2026/from-one-game-to-a-catalog-how-solo-unity-developers-actually-scale-their-mobile-revenue-4bd96a03fc9a)
- [DEV — Google Play I/O 2026 from a solo indie puzzle developer](https://dev.to/lumaplay/what-google-plays-io-2026-updates-look-like-from-a-solo-indie-puzzle-developer-51kp)

> ⚠️ 장르별 매출 수치는 AppMagic·Sensor Tower 기반 2차 자료가 많고 출처끼리 다른 경우가 있음(예: Sort 퍼즐 성장률 46%~229%). 방향성 판단용.
