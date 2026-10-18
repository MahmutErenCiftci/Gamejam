// Akşam sel — çevre tehlikeleri tanımı (FLOOD-MECH, 18 Eki): elektrik direkleri + teller, kopuk teller (tehlike bölgeleri),
// yolda duran enkaz (yüzen hafif cisimler + yüzmeyen taşlar). Izgara koordinatı (gx, gy); dünya: x=(gx-gy)/2, y=(gx+gy)/4.
// Hem hazards.js (sprite'lar) hem layout.js (yerleşim + çapalar) bunu okur. Kural: tehlike bölgeleri sokağın tamamını kapatmaz,
// her bölgenin yanında en az ~1,8 hücrelik açık şerit kalır; enkaz güvenli yolu kesmez (yürünebilir, katı değil).

// Direkler evlerin olduğu ARKA kaldırımda (lambalarla aynı hizada). axis = tel hattının yönü ('x' = gx boyunca, 'y' = gy boyunca);
// travers (kol) hatta diktir.
const POLES = {
  A1: [-9.0, 4.55, 'x'], A2: [-2.2, 4.55, 'x'], A3: [5.0, 4.55, 'x'], A4: [12.6, 4.55, 'x'],
  B1: [15.55, 8.0, 'y'], B2: [15.55, 14.6, 'y'],
  F1: [29.55, 23.4, 'y'], F2: [29.55, 27.4, 'y'],
  G1: [31.6, 30.55, 'x'], G2: [35.1, 30.55, 'x'], G3: [38.6, 30.55, 'x'],
  H1: [45.55, 25.2, 'y'], H2: [45.55, 20.4, 'y'],
  I1: [53.2, 17.55, 'x'], I2: [57.8, 17.55, 'x'],
  J1: [58.55, 9.6, 'y'], J2: [58.55, 3.8, 'y'],
};
// Travers: insülatör tepeleri direk ekseninden ±ARM hücre, yerden WIRE_Z piksel yukarıda.
const ARM = 11 / 32, WIRE_Z = 90;
// Açıklıklar: [a, b, teller] — teller: [-1, +1] iki tel; kopuk tel o açıklıktan çıkarılır (aşağıdaki SNAPPED).
const SPANS = [
  ['A1', 'A2', [-1, 1]], ['A2', 'A3', [-1, 1]], ['A3', 'A4', [1]],
  ['B1', 'B2', [-1, 1]], ['F1', 'F2', [-1, 1]],
  ['G1', 'G2', [-1, 1]], ['G2', 'G3', [1]],
  ['H1', 'H2', [1]], ['I1', 'I2', [-1, 1]], ['J1', 'J2', [-1, 1]],
];
// Kopuk teller: direğin `side` insülatöründen yola sarkar, ucu `contact` noktasında suya/birikintiye değer.
// core: kablo ucunun çevresi (hücre; su olmasa da her zaman tehlikeli), water: su varken etkin bölge yarıçapı (hücre).
// safe: geri itmenin hedeflendiği, bölgenin dışındaki açık noktalar (oyuncunun geldiği tarafa en yakın olan seçilir).
const SNAPPED = [
  { id: 'Zone_1', pole: 'A3', side: -1, contact: [6.3, 2.5], core: 0.4, water: 1.0,
    safe: [[6.3, 4.05], [6.3, 0.95], [4.45, 2.55], [8.15, 2.55]] },
  { id: 'Zone_2', pole: 'G3', side: -1, contact: [40.2, 28.8], core: 0.4, water: 1.0,
    safe: [[40.2, 27.2], [40.3, 30.25], [38.35, 28.8], [42.05, 28.8]] },
  { id: 'Zone_3', pole: 'H2', side: -1, contact: [44.0, 21.8], core: 0.4, water: 1.0,
    safe: [[42.4, 21.8], [44.0, 23.65], [44.0, 19.95]] },
];
// Enkaz: hafif (yüzer) ve ağır (yüzmez). Çoğu, sel %30'u geçince su gelmiş olan sokaklarda (A, B, C, kavşak, F, G).
const DEBRIS = [
  ['Plank', 3.0, 1.2], ['Crate', -2.6, 0.9], ['Bottle', 9.6, 2.3], ['Branch', 12.2, 1.4],
  ['Plank', 12.6, 9.2], ['Bottle', 14.3, 13.1],
  ['Crate', 19.6, 13.6], ['Branch', 26.2, 12.2],
  ['Plank', 26.4, 25.6], ['Bottle', 28.3, 22.1],
  ['Crate', 33.6, 27.9], ['Branch', 43.6, 27.1],
  ['Plank', 42.4, 16.1], ['Bottle', 57.0, 14.6],
  ['Stone', -4.6, 1.7], ['Rubble', 7.9, 0.7], ['Stone', 13.4, 10.6], ['Stone', 21.6, 13.1],
  ['Rubble', 27.6, 27.1], ['Stone', 31.3, 27.3], ['Stone', 44.8, 14.7], ['Rubble', 57.4, 7.1],
];
const LIGHT_KINDS = ['Plank', 'Crate', 'Bottle', 'Branch'];

module.exports = { POLES, ARM, WIRE_Z, SPANS, SNAPPED, DEBRIS, LIGHT_KINDS };
