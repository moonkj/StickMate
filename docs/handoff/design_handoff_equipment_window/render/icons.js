// ItemIcon.dc.html 의 React 벡터 정의를 순수 문자열 SVG 생성기로 옮긴 것.
// 좌표·알파·규칙은 원문 그대로. 검증: kinds 16종 · 조각 수 원문과 대조.
let UID = 0;
function ItemIcon(kind, color, accent, stroke) {
  const uid = 'ic' + (++UID);
  const C = color || '#E8E2D6';
  const MAT = { shortcape: '#D2402F', longcape: '#D2402F' };
  const A = MAT[kind] || accent || '#C8A15A';
  const w = stroke || 2.2;
  const G = uid + '-body';
  const at = (o) => Object.keys(o).map(k => `${k}="${o[k]}"`).join(' ');
  const B = (d, o) => `<path d="${d}" fill="url(#${G})" stroke="${C}" stroke-width="${w}" stroke-linejoin="round" stroke-linecap="round" ${at(o || {})}/>`;
  const S = (d, o) => `<path d="${d}" fill="none" stroke="${C}" stroke-width="${w}" stroke-linejoin="round" stroke-linecap="round" ${at(o || {})}/>`;
  const F = (d, o) => `<path d="${d}" fill="${A}" fill-opacity="0.55" ${at(o || {})}/>`;
  const H = (d, o) => `<path d="${d}" fill="none" stroke="#FFFFFF" stroke-opacity="0.42" stroke-width="${w * 0.75}" stroke-linecap="round" ${at(o || {})}/>`;
  const CB = (cx, cy, r, o) => `<circle cx="${cx}" cy="${cy}" r="${r}" fill="url(#${G})" stroke="${C}" stroke-width="${w}" ${at(o || {})}/>`;
  const CF = (cx, cy, r, o) => `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${A}" ${at(o || {})}/>`;
  const RB = (x, y, ww, hh, r, o) => `<rect x="${x}" y="${y}" width="${ww}" height="${hh}" rx="${r}" fill="url(#${G})" stroke="${C}" stroke-width="${w}" ${at(o || {})}/>`;

  const K = {
    clothhat: () => [
      B('M18 41 Q16.5 21 32 21 Q47.5 21 46 41 Z'),
      F('M19.4 33.5 Q32 30 44.6 33.5 L45 39 Q32 35.6 19 39 Z', { 'fill-opacity': 0.5 }),
      B('M7 41 Q32 33.5 57 41 Q32 51 7 41 Z'),
      H('M23.5 27 Q26.5 23.4 31 23')
    ],
    furhat: () => [
      CB(32, 11.5, 4.6),
      B('M20.5 35 Q20.5 13.5 32 13.5 Q43.5 13.5 43.5 35 Z'),
      H('M25 22 Q26.5 17.5 30.5 16'),
      B('M12 35.5 Q32 31 52 35.5 Q54.5 41 52 47 Q32 51.5 12 47 Q9.5 41 12 35.5 Z', { fill: A, 'fill-opacity': 0.22 }),
      S('M16 39.5 Q20 43 24 39.5 Q28 43 32 39.5 Q36 43 40 39.5 Q44 43 48 39.5', { 'stroke-width': w * 0.8, 'stroke-opacity': 0.55 })
    ],
    fedora: () => [
      B('M20 41 Q18.5 18 26 16.5 Q29 24 32 24 Q35 24 38 16.5 Q45.5 18 44 41 Z'),
      F('M20 33.6 Q32 30.4 44 33.6 L44.3 39 Q32 35.8 19.7 39 Z'),
      B('M6 41.5 Q32 33 58 41.5 Q32 51.5 6 41.5 Z'),
      H('M24 25 Q25.5 20 28 18.5')
    ],
    crown: () => [
      B('M13 41 L15 17 L23.5 29 L32 13.5 L40.5 29 L49 17 L51 41 Z'),
      B('M12.5 40.5 H51.5 L52.5 47 Q32 51.5 11.5 47 Z'),
      CF(32, 44, 2.8), CF(21, 44.4, 1.9), CF(43, 44.4, 1.9),
      CB(32, 15, 2.6, { fill: A }), CB(15, 18.5, 2.1, { fill: A }), CB(49, 18.5, 2.1, { fill: A }),
      H('M17.5 24 L21 29')
    ],
    sunglasses: () => [
      B('M6 24 H28 Q29 24 28.8 26 L27 36 Q26.4 40 22 40 H13 Q9 40 8 36.5 L5.4 26 Q5 24 6 24 Z', { fill: A, 'fill-opacity': 0.6 }),
      B('M58 24 H36 Q35 24 35.2 26 L37 36 Q37.6 40 42 40 H51 Q55 40 56 36.5 L58.6 26 Q59 24 58 24 Z', { fill: A, 'fill-opacity': 0.6 }),
      S('M28.4 26.5 Q32 24.4 35.6 26.5'),
      S('M5.6 25 Q2 25.5 1.5 29'), S('M58.4 25 Q62 25.5 62.5 29'),
      H('M10 28 L15 34.5')
    ],
    roundglasses: () => [
      CB(19, 31, 10.5, { fill: A, 'fill-opacity': 0.16 }),
      CB(45, 31, 10.5, { fill: A, 'fill-opacity': 0.16 }),
      S('M29.4 29.8 Q32 26.8 34.6 29.8'),
      S('M8.6 28 Q3 26 1.5 30.5'), S('M55.4 28 Q61 26 62.5 30.5'),
      H('M14 27 Q16.5 24.5 20 24.2'), H('M40 27 Q42.5 24.5 46 24.2')
    ],
    goggles: () => [
      B('M11 23 H53 Q57 23 57 28 V37 Q57 42 53 42 H11 Q7 42 7 37 V28 Q7 23 11 23 Z'),
      CB(20.5, 32.5, 7, { fill: A, 'fill-opacity': 0.6 }),
      CB(43.5, 32.5, 7, { fill: A, 'fill-opacity': 0.6 }),
      S('M30 32.5 H34', { 'stroke-width': w * 1.4 }),
      S('M7 28.5 Q1.5 30 1.5 35 Q1.5 39 4 40.5'), S('M57 28.5 Q62.5 30 62.5 35 Q62.5 39 60 40.5'),
      H('M17 28.5 Q19.5 27 22 27.4')
    ],
    monocle: () => [
      CB(25, 27, 12.5, { fill: A, 'fill-opacity': 0.2, 'stroke-width': w * 1.5 }),
      H('M18.5 21 Q21 18.4 25 18'),
      S('M35.5 34.5 Q42 43 43.5 51', { 'stroke-width': w * 0.8, 'stroke-dasharray': '0.5 3.6' }),
      CB(44, 54, 3.2, { fill: A })
    ],
    bowtie: () => [
      B('M29.5 32 L10.5 20.5 Q8 19 8 22 V42 Q8 45 10.5 43.5 Z'),
      B('M34.5 32 L53.5 20.5 Q56 19 56 22 V42 Q56 45 53.5 43.5 Z'),
      RB(27, 25.5, 10, 13, 3.5, { fill: A, 'fill-opacity': 0.65 }),
      H('M13 24 V30')
    ],
    stripedtie: () => [
      B('M25 8 H39 L35.5 19 H28.5 Z'),
      B('M28.6 19 H35.4 L38.5 42.5 L32 56 L25.5 42.5 Z'),
      F('M27.4 27.5 L36.2 25.5 L36.6 29.4 L27.8 31.4 Z'),
      F('M28.4 36 L37.2 34 L37.6 37.9 L28.8 39.9 Z'),
      H('M28.5 11 L30 16')
    ],
    scarf: () => [
      B('M10 20 Q32 32.5 54 20 L54 28.5 Q32 41 10 28.5 Z'),
      B('M39.5 35.5 Q46 45 43.5 55 L34 53 Q37.5 44.5 35 34'),
      S('M34.3 53.6 L33 58.5'), S('M38 54.4 L37 59'), S('M42 54.6 L41.6 59'),
      H('M15 24.5 Q20 27.6 25 29')
    ],
    bellnecklace: () => [
      S('M11 18 Q32 39 53 18', { 'stroke-width': w * 0.9 }),
      CF(20, 27, 1.7, { 'fill-opacity': 0.8 }), CF(32, 32.5, 1.7, { 'fill-opacity': 0.8 }), CF(44, 27, 1.7, { 'fill-opacity': 0.8 }),
      B('M32 34 Q24 34.5 23 44 H41 Q40 34.5 32 34 Z', { fill: A, 'fill-opacity': 0.4 }),
      RB(21.5, 43.5, 21, 4.5, 2.2, { fill: A, 'fill-opacity': 0.55 }),
      CB(32, 50.5, 2.6, { fill: A }),
      H('M27 38.5 Q27.5 35.5 30 34.6')
    ],
    shortcape: () => [
      B('M20.5 15.5 Q11 30 12 43 Q17.5 47.5 22 43.5 Q27 48 32 44 Q37 48 42 43.5 Q46.5 47.5 52 43 Q53 30 43.5 15.5 Z'),
      B('M21 12 Q32 19 43 12 L44.5 17 Q32 24.5 19.5 17 Z', { fill: A, 'fill-opacity': 0.45 }),
      S('M32 20 V44', { 'stroke-width': w * 0.7, 'stroke-opacity': 0.4 }),
      H('M24.5 20 Q19 29 18.5 38')
    ],
    longcape: () => [
      B('M21 12 Q7 34 9 55 Q17 60 24 56 Q28 61 32 57 Q36 61 40 56 Q47 60 55 55 Q57 34 43 12 Z'),
      B('M21.5 8.5 Q32 15.5 42.5 8.5 L44 14 Q32 21.5 20 14 Z', { fill: A, 'fill-opacity': 0.45 }),
      CB(32, 12.5, 2.6, { fill: A }),
      S('M25 17 Q22 36 22.5 56', { 'stroke-width': w * 0.7, 'stroke-opacity': 0.35 }),
      S('M39 17 Q42 36 41.5 56', { 'stroke-width': w * 0.7, 'stroke-opacity': 0.35 }),
      H('M24.5 18 Q17 32 15.5 45')
    ],
    wings: () => [
      B('M31 11 Q17 12 9 24 Q17 24 20 29 Q13 30 8 36 Q17 35 21 40 Q26 34 30 30 Z'),
      B('M33 11 Q47 12 55 24 Q47 24 44 29 Q51 30 56 36 Q47 35 43 40 Q38 34 34 30 Z'),
      S('M30 18 Q24 20 20 24', { 'stroke-width': w * 0.7, 'stroke-opacity': 0.4 }),
      S('M34 18 Q40 20 44 24', { 'stroke-width': w * 0.7, 'stroke-opacity': 0.4 }),
      RB(30.4, 12, 3.2, 32, 1.6, { fill: A, 'fill-opacity': 0.4 }),
      H('M14 23 Q18.5 19 24 17')
    ],
    backpack: () => [
      S('M22 22 Q22 10 32 10 Q42 10 42 22', { 'stroke-width': w * 1.1 }),
      B('M17 19 H47 Q52 19 52 25 V48 Q52 54 46 54 H18 Q12 54 12 48 V25 Q12 19 17 19 Z'),
      B('M17 19 H47 Q52 19 52 26 Q52 33 46 33 H18 Q12 33 12 26 Q12 19 17 19 Z', { fill: A, 'fill-opacity': 0.28 }),
      RB(27, 29, 10, 7, 2.2, { fill: A, 'fill-opacity': 0.7 }),
      RB(23, 39, 18, 11, 3, { fill: A, 'fill-opacity': 0.14 }),
      H('M16.5 24 V30')
    ]
  };
  const kids = (K[kind] || K.clothhat)().join('');
  const isMat = !!MAT[kind];
  const defs = `<defs><linearGradient id="${G}" x1="0" y1="0" x2="0.35" y2="1">` +
    `<stop offset="0%" stop-color="${A}" stop-opacity="${isMat ? 1 : 0.34}"/>` +
    `<stop offset="100%" stop-color="${A}" stop-opacity="${isMat ? 0.62 : 0.08}"/></linearGradient></defs>`;
  return `<svg viewBox="0 0 64 64" width="100%" height="100%" style="display:block;overflow:visible">${defs}${kids}</svg>`;
}
if (typeof module !== 'undefined') module.exports = { ItemIcon };
