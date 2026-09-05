// equipment-screen.dc.html script #2 의 데이터·파생 로직을 그대로 옮긴 것 (시안 1a 전용).
const SLOT_MAIN = { HEAD: '집중력', EYES: '관찰력', NECK: '매력', BACK: '민첩' };
const THEME = { office: '오피스 워커', cyber: '사이버 아포칼립스', neon: '네온 낙서', sport: '스포츠 이펙트', ink: '컬러 잉크', mil: '밀리터리' };
const MAINV = [3, 6, 10, 15];
const SUBV = [1, 2, 4, 6];
const CATS = [
  { key: 'HEAD', ko: '모자', en: 'HEAD', main: '집중력', items: [
    { id: 'h1', name: '천모자', kind: 'clothhat', rar: 0, owned: true, theme: 'ink', sub: '매력', price: 600, desc: '수수한 천모자. 집중모드 성과물이 조금 더 쌓인다.' },
    { id: 'h2', name: '털모자', kind: 'furhat', rar: 1, owned: true, theme: 'sport', sub: '민첩', price: 1400, desc: '두툼한 털모자. 유휴 졸음 연출이 자주 나온다.' },
    { id: 'h3', name: '중절모', kind: 'fedora', rar: 1, owned: true, theme: 'office', sub: '관찰력', price: 1400, desc: '단정하게 접힌 중절모. 책상업무 성과물 배율이 오른다.' },
    { id: 'h4', name: '왕관', kind: 'crown', rar: 3, owned: false, dlc: true, theme: 'cyber', sub: '매력', desc: '사이버 아포칼립스 팩 시그니처. 집중력 임계를 단번에 끌어올린다.' } ] },
  { key: 'EYES', ko: '안경', en: 'EYES', main: '관찰력', items: [
    { id: 'e1', name: '선글라스', kind: 'sunglasses', rar: 2, owned: true, theme: 'mil', sub: '민첩', price: 3200, desc: '짙게 코팅된 렌즈. 활쏘기 자동발동 쿨다운이 짧아진다.' },
    { id: 'e2', name: '동그란안경', kind: 'roundglasses', rar: 0, owned: true, theme: 'office', sub: '집중력', price: 600, desc: '얇은 금속테. 화살 궤적을 조금 더 정확히 읽는다.' },
    { id: 'e3', name: '고글', kind: 'goggles', rar: 1, owned: true, theme: 'cyber', sub: '민첩', price: 1400, desc: '방풍 고글. 창 던전 파밍 중 시야가 흐려지지 않는다.' },
    { id: 'e4', name: '외알안경', kind: 'monocle', rar: 2, owned: false, theme: 'ink', sub: '집중력', price: 3200, desc: '감정용 렌즈. 신규 화살 잔상 이펙트가 해금된다.' } ] },
  { key: 'NECK', ko: '넥타이', en: 'NECK', main: '매력', items: [
    { id: 'n1', name: '나비넥타이', kind: 'bowtie', rar: 1, owned: true, theme: 'office', sub: '집중력', price: 1400, desc: '연회용 나비넥타이. 주변 오라 범위가 넓어진다.' },
    { id: 'n2', name: '줄무늬타이', kind: 'stripedtie', rar: 0, owned: true, theme: 'office', sub: '집중력', price: 600, desc: '기본 사무용 넥타이. 오피스 워커 세트의 기본 부품.' },
    { id: 'n3', name: '목도리', kind: 'scarf', rar: 0, owned: true, theme: 'sport', sub: '민첩', price: 600, desc: '손뜨개 목도리. 겨울 유휴 동작이 추가된다.' },
    { id: 'n4', name: '방울목걸이', kind: 'bellnecklace', rar: 2, owned: true, theme: 'neon', sub: '관찰력', price: 3200, desc: '움직일 때마다 소리가 난다. 오라 이펙트 강도가 한 단계 올라간다.' } ] },
  { key: 'BACK', ko: '망토', en: 'BACK', main: '민첩', items: [
    { id: 'b1', name: '짧은망토', kind: 'shortcape', rar: 0, owned: true, theme: 'mil', sub: '집중력', price: 600, desc: '어깨를 덮는 짧은 망토. 이동속도가 조금 오른다.' },
    { id: 'b2', name: '긴망토', kind: 'longcape', rar: 1, owned: true, theme: 'cyber', sub: '매력', price: 1400, desc: '발끝까지 흐르는 망토. 던지기 회전 잔상이 길게 남는다.' },
    { id: 'b3', name: '날개', kind: 'wings', rar: 3, owned: false, dlc: true, theme: 'neon', sub: '관찰력', desc: '네온 낙서 팩 시그니처. 최소 회전수가 보장되고 트레일이 팩 색으로 바뀐다.' },
    { id: 'b4', name: '배낭', kind: 'backpack', rar: 0, owned: true, theme: 'office', sub: '집중력', price: 600, desc: '튼튼한 배낭. 파쿠르 착지 시 소품이 흔들린다.' } ] }
];
const RAR_A = [ { l: '일반', c: '#8A8F98' }, { l: '희귀', c: '#6E9BE8' }, { l: '영웅', c: '#B07BE0' }, { l: '전설', c: '#E0B24A' } ];
const WEAR_A = ['#D8B27A', '#7FB0F2', '#C08FEC', '#F0C25C'];
const STAT_ORDER = ['집중력', '관찰력', '매력', '민첩'];
const STAT_SLOT = { 집중력: '모자', 관찰력: '안경', 매력: '넥타이', 민첩: '망토' };
const BASE = { 집중력: 8, 관찰력: 6, 매력: 5, 민첩: 7 };
const TIERS = [{ l: '초급', v: 10 }, { l: '중급', v: 20 }, { l: '고급', v: 32 }];
const CAP = 40;
const EFFECT = {
  집중력: ['성과물 배율 +10%', '명상 유휴 연출 해금', '성과물 배율 +25% · 졸음 연출'],
  관찰력: ['활쏘기 쿨다운 −10%', '궤적 잔상 이펙트', '쿨다운 −25% · 잔상 강화'],
  매력: ['오라 이펙트 발현', '오라 범위 확대', '오라 강도 최대 · 팩 색 연동'],
  민첩: ['이동속도 +8%', '던지기 최소 회전 보장', '이동속도 +18% · 트레일 이펙트']
};
const findItem = (id) => { for (const c of CATS) { const it = c.items.find(x => x.id === id); if (it) return { it, cat: c }; } return null; };
const EQ = { HEAD: 'h3', EYES: 'e2', NECK: 'n2', BACK: 'b2' };
const SEL = 'b4';

function build() {
  const RAR = RAR_A, eq = EQ, sel = SEL;
  const cats = CATS.map(c => {
    const items = c.items.map(it => {
      const r = RAR[it.rar], equipped = eq[c.key] === it.id, isSel = sel === it.id, dim = !it.owned;
      return { id: it.id, name: it.name, kind: it.kind, rarLabel: r.l, rc: r.c,
        ribbon: dim ? 0.25 : 1, op: dim ? 0.34 : 1,
        nc: dim ? '#6E665C' : '#EDE7DB', ic: dim ? '#6E665C' : '#E8E2D6',
        bg: equipped ? 'linear-gradient(180deg,#1D1813,#141110)' : 'linear-gradient(180deg,#161311,#111010)',
        bc: equipped ? '#C8A15A' : (isSel ? '#4A4036' : '#231F1B'),
        shadow: equipped ? '0 0 0 1px rgba(200,161,90,0.25), 0 10px 24px -14px rgba(200,161,90,0.6)' : 'none',
        iconBg: 'radial-gradient(70% 70% at 50% 42%, ' + r.c + '1F, transparent 72%), #0F0D0C',
        mainText: c.main + ' +' + MAINV[it.rar], subText: it.sub + ' +' + SUBV[it.rar],
        themeKo: THEME[it.theme],
        priceText: it.dlc ? 'DLC 전용' : (it.owned ? '보유' : '동전 ' + it.price.toLocaleString()),
        btnLabel: it.dlc && !it.owned ? '스토어에서 구매' : (!it.owned ? '동전 ' + it.price.toLocaleString() : (equipped ? '해제' : '착용')),
        btnBg: !it.owned ? '#141210' : (equipped ? '#2A2119' : '#C8A15A'),
        btnColor: !it.owned ? '#5C574E' : (equipped ? '#C8A15A' : '#160F06'),
        btnBorder: !it.owned ? '#231F1B' : (equipped ? '#4A3A26' : 'transparent') };
    });
    const owned = c.items.filter(i => i.owned).length;
    return { ko: c.ko, en: c.en, main: c.main, count: owned + '/' + c.items.length, items };
  });
  const slots = CATS.map(c => {
    const id = eq[c.key], f = id ? findItem(id) : null;
    return { ko: c.ko, main: c.main, kind: f ? f.it.kind : c.items[0].kind,
      name: f ? f.it.name : '비어 있음', mainText: f ? c.main + ' +' + MAINV[f.it.rar] : '—',
      bc: f ? '#C8A15A' : '#231F1B', bg: f ? '#17130E' : '#111010',
      nc: f ? '#EDE7DB' : '#5C574E', ic: f ? '#E8E2D6' : '#3A342D', op: f ? 1 : 0.5 };
  });
  const f = findItem(sel), r = RAR[f.it.rar], equipped = eq[f.cat.key] === f.it.id;
  const detail = { name: f.it.name, kind: f.it.kind, desc: f.it.desc,
    stats: [f.cat.main + ' +' + MAINV[f.it.rar] + ' (주)', f.it.sub + ' +' + SUBV[f.it.rar] + ' (부)'],
    themeKo: THEME[f.it.theme], priceText: f.it.dlc ? 'DLC 전용 · 스토어 구매' : '동전 ' + (f.it.price || 0).toLocaleString(),
    rarLabel: r.l, rc: r.c, slotKo: f.cat.ko,
    mainLabel: f.it.dlc && !f.it.owned ? '스토어에서 구매' : (!f.it.owned ? '동전 ' + f.it.price.toLocaleString() + '으로 해금' : (equipped ? '해제하기' : '착용하기')),
    mainBg: !f.it.owned ? '#17130E' : (equipped ? '#221B14' : '#C8A15A'),
    mainColor: !f.it.owned ? '#C8A15A' : (equipped ? '#C8A15A' : '#160F06'),
    mainBorder: !f.it.owned ? '#3A3026' : 'transparent' };
  const at = (k) => { const id = eq[k]; const g = id ? findItem(id) : null;
    if (!g) return { kind: null, ac: '#C8A15A', pc: '#C8A15A' };
    return { kind: g.it.kind, ac: RAR[g.it.rar].c, pc: WEAR_A[g.it.rar] }; };
  const head = at('HEAD'), eyes = at('EYES'), neck = at('NECK'), back = at('BACK');
  const isCape = back.kind === 'shortcape' || back.kind === 'longcape';
  const bonus = { 집중력: 0, 관찰력: 0, 매력: 0, 민첩: 0 }; const themeCount = {};
  CATS.forEach(c => { const id = eq[c.key]; if (!id) return; const g = findItem(id);
    bonus[c.main] += MAINV[g.it.rar]; bonus[g.it.sub] += SUBV[g.it.rar];
    themeCount[g.it.theme] = (themeCount[g.it.theme] || 0) + 1; });
  const statRows = STAT_ORDER.map(n => {
    const total = BASE[n] + bonus[n]; let stage = -1;
    TIERS.forEach((t, i) => { if (total >= t.v) stage = i; });
    const next = TIERS[stage + 1];
    return { name: n, slotKo: STAT_SLOT[n], total,
      bonusText: bonus[n] > 0 ? '+' + bonus[n] : '—', bonusColor: bonus[n] > 0 ? '#8FBF6A' : '#5C574E',
      stageLabel: stage < 0 ? '미달' : TIERS[stage].l,
      stageColor: stage < 0 ? '#5C574E' : ['#9AA1AB', '#C8A15A', '#E0B24A'][stage],
      effect: stage < 0 ? '임계 미달 · 효과 없음' : EFFECT[n][stage],
      nextText: next ? next.l + '까지 ' + (next.v - total) : '최고 단계',
      pct: Math.min(100, Math.round(total / CAP * 100)) + '%',
      t1: Math.round(TIERS[0].v / CAP * 100) + '%', t2: Math.round(TIERS[1].v / CAP * 100) + '%', t3: Math.round(TIERS[2].v / CAP * 100) + '%' };
  });
  let topTheme = null, topN = 0;
  Object.keys(themeCount).forEach(t => { if (themeCount[t] > topN) { topN = themeCount[t]; topTheme = t; } });
  const complete = topN === 4;
  const setPanel = { themeKo: topTheme ? THEME[topTheme] : '테마 없음', countText: topN + '/4',
    dots: [0, 1, 2, 3].map(i => ({ bg: i < topN ? '#C8A15A' : '#2A2622' })),
    statusText: complete ? '세트 완성 · 활성' : '미완성 · 기본 중립 대사',
    statusColor: complete ? '#8FBF6A' : '#8A8578',
    rows: [ { label: '부분 착용 플레이버', desc: '개별 아이템 유휴 동작 (거수경례 등)', active: topN > 0 },
            { label: '대사 풀 전환', desc: '테마 전용 대사로 교체', active: complete },
            { label: '연출 스킨 교체', desc: '격파 성공 시 테마 이펙트', active: complete },
            { label: '스탯 총합 보너스', desc: '4부위 합계 +8', active: complete } ]
      .map(rr => ({ label: rr.label, desc: rr.desc, color: rr.active ? '#EDE7DB' : '#5C574E',
        mark: rr.active ? '●' : '○', markColor: rr.active ? '#C8A15A' : '#332E28' })) };
  const ownedTotal = CATS.reduce((t, c) => t + c.items.filter(i => i.owned).length, 0);
  const allTotal = CATS.reduce((t, c) => t + c.items.length, 0);
  return { cats, slots, detail, statRows, setPanel,
    power: STAT_ORDER.reduce((t, n) => t + bonus[n], 0), ownedTotal, allTotal,
    eq: { head: head.kind, headAc: head.ac, headPc: head.pc, eyes: eyes.kind, eyesAc: eyes.ac, eyesPc: eyes.pc,
      neck: neck.kind, neckAc: neck.ac, neckPc: neck.pc, back: isCape ? null : back.kind, backAc: back.ac, backPc: back.pc,
      capeLong: back.kind === 'longcape', capeShort: back.kind === 'shortcape' } };
}
