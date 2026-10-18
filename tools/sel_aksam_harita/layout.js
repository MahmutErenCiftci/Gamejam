// Mahalle yerleşimi v3 (zikzak): sprite'lar, ışıklar, çapalar, collider'lar (ızgara koordinatı). Dünya: x=(gx-gy)/2, y=(gx+gy)/4 + h/64.
// Kural: her koridorun ÖN tarafında (düşük gx / düşük gy) alçak şeyler (bahçe duvarı, çit, çalı), ARKA tarafında evler.
const M = require('./map.js');
const fl = Math.floor;
const hOf = (gx, gy) => M.H(fl(gx), fl(gy));
const objs = [];
const dia = r => [[-r, 0], [0, -r], [r, 0], [0, r]];
const rectC = (a, b) => [[-a, -b], [a, -b], [a, b], [-a, b]];
function P(group, name, s, gx, gy, o = {}) { objs.push(Object.assign({ group, name, s, gx, gy, h: o.h !== undefined ? o.h : hOf(gx, gy), order: 0 }, o)); }
const ST = 'A:Props/Street/', SU = 'A:Props/Street/Surroundings/', SR = 'A:Props/SchoolRoute/', AS = 'A:Props/Assembly/';
const bodyOf = { Facade_A: 'Body_A', Facade_B: 'Body_B', Facade_Home: 'Body_Home' };
const FAC = ['Facade_Home', 'Facade_A', 'Facade_B'];
let hi = 0;
// gx yönünde dizili ev (cephe -gy'ye bakar): sol-ön köşe (gx, gy)
const houseX = (gx, gy, f) => { f = f || FAC[hi++ % 3]; P('Buildings', f + '_' + gx + '_' + gy, ST + 'PROP_Street_' + f, gx, gy, { order: -50, h: 0 }); P('Buildings', 'Body_' + gx + '_' + gy, SU + 'PROP_StreetBG_' + bodyOf[f], gx, gy, { order: -62, h: 0 }); };
// gy yönünde dizili ev (cephe -gx'e bakar, aynalı)
const houseY = (gx, gy, f) => { f = f || FAC[hi++ % 3]; P('Buildings', f + '_' + gx + '_' + gy, ST + 'PROP_Street_' + f, gx, gy, { order: -50, h: 0, flip: true }); P('Buildings', 'Body_' + gx + '_' + gy, SU + 'PROP_StreetBG_' + bodyOf[f], gx, gy, { order: -62, h: 0, flip: true }); };
const lamp = (n, gx, gy) => P('Props', 'Lamp_' + n, ST + 'PROP_Street_Lamp', gx, gy, { col: dia(0.14), light: true });
const tree = (n, s, gx, gy) => P('Yard', n, SU + 'PROP_StreetBG_' + s, gx, gy, { order: -60 });
const decal = (n, s, gx, gy) => P('Decals', n, ST + 'PROP_Street_' + s, gx, gy, { order: -90 });
const fenceX = (gx, gy) => P('Buildings', 'Fence_' + gx + '_' + gy, ST + 'PROP_Street_Fence', gx, gy, { order: -50, h: 0 });
const fenceY = (gx, gy) => P('Buildings', 'FenceY_' + gx + '_' + gy, ST + 'PROP_Street_Fence', gx, gy, { order: -50, h: 0, flip: true });

// ================================================================ A: başlangıç sokağı (eski selin sokağı görselleri)
for (const gx of [-22, -17, -12, -7, -2]) houseX(gx, 5);
for (const gx of [2.5, 4, 5.5, 7]) fenceX(gx, 5); tree('A_Tree1', 'Tree_Round', 6, 7.5);
[[3, -25], [1, -19], [2, -13], [3, -7], [1, -1]].forEach(([k, gx], i) => P('Buildings', 'Apt_A' + i, SU + 'PROP_StreetBG_Apt_' + k, gx, 12, { order: -70, h: 0 }));
for (const gx of [-14, -5, 4]) P('Props', 'VergeTree_' + gx, ST + 'PROP_Street_Tree', gx + 0.5, -1.1);
lamp('A1', -6, 4.6); lamp('A2', 2, 4.6); lamp('A3', 10.6, 4.6);       // A3: köşeden ara sokağı gösteren lamba
P('Props', 'Bin_A', ST + 'PROP_Street_TrashBin', -3.4, 4.55, { col: dia(0.2) });
P('Props', 'BusStop', ST + 'PROP_Street_BusStop', -1.0, 4.4, { col: rectC(0.15, 0.6), order: -2 });
P('Props', 'BusStopRoof', ST + 'PROP_Street_BusStop_Roof', -1.0, 4.4, { order: 5 });
P('Props', 'ParkedCar_1', SU + 'PROP_StreetBG_Car_C', 5.5, 0.55, { col: rectC(0.9, 0.35) });
P('Props', 'ParkedCar_End', SU + 'PROP_StreetBG_Car_A', 14.6, 1.0, { col: rectC(0.9, 0.35) }); // sokak sonu: araba + duvar → ara sokağa dön
for (const [n, s, gx, gy] of [['Drain_1', 'Drain', -6, 2.6], ['Drain_2', 'Drain', 6, 2.6], ['Puddle_1', 'Puddle_M', -3, 1.2], ['Puddle_2', 'Puddle_S', 1, 3.6], ['Puddle_3', 'Puddle_L', 9, 1.6], ['Puddle_4', 'Puddle_S', 12.5, 3.4]]) decal(n, s, gx, gy);

// ================================================================ B: ara sokak (+gy)
for (const gy of [5, 6.2, 7.4, 8.6, 9.8]) fenceY(16, gy); tree('B_Yard1', 'Tree_Round', 19, 8); tree('B_Yard2', 'Bush', 17.5, 6.5);
lamp('B1', 15.6, 11.5);
tree('B_Bush1', 'Bush', 9.5, 7); tree('B_Tree1', 'Tree_Small', 9.0, 10.5); tree('B_Bush2', 'Bush', 9.8, 14);
for (const [n, s, gx, gy] of [['BPuddle_1', 'Puddle_M', 13.5, 8], ['BDrain_1', 'Drain', 12, 12], ['BPuddle_2', 'Puddle_S', 14.5, 15]]) decal(n, s, gx, gy);

// ================================================================ C: (+gx) kavşağa
houseX(11, 17); houseX(15.4, 17); fenceX(19.8, 17); tree('C_BackTree', 'Tree_Pine', 22, 19.5);
tree('C_Bush1', 'Bush', 18, 10.6); tree('C_Tree1', 'Tree_Round', 20.5, 9.5); tree('C_Swing', 'Swing', 22.5, 7.5);
lamp('C1', 22.6, 16.6);
for (const [n, s, gx, gy] of [['CPuddle_1', 'Puddle_L', 18.5, 14.5], ['CDrain_1', 'Drain', 21, 13.3]]) decal(n, s, gx, gy);

// ================================================================ KAVŞAK meydanı + tabelalar
houseY(31, 10, 'Facade_A'); houseY(31, 14.3, 'Facade_B');
P('Signs', 'Sign_Kisa', 'N:PROP_FE_Sign_Kisa', 29.6, 10.6, { col: dia(0.1) });     // D ağzı (aşağı-sağa)
P('Signs', 'Sign_Uzun', 'N:PROP_FE_Sign_Uzun', 25.6, 18.4, { col: dia(0.1) });     // F ağzı (yukarı-sola)
lamp('Fork1', 23.6, 18.6); lamp('Fork2', 30.6, 17.8);
P('Props', 'ForkBench', AS + 'PROP_Assembly_Bench', 24.2, 15.8, { col: rectC(0.5, 0.25) });
for (const [n, s, gx, gy] of [['FPuddle_1', 'Puddle_M', 27, 12.5], ['FPuddle_2', 'Puddle_S', 28.5, 16.8]]) decal(n, s, gx, gy);

// ================================================================ KISA YOL: D (-gy) → köşe → E (+gx) → yıkıntı, ötesi derin sel
for (const gx of [30, 31.4, 32.8, 34.2]) fenceX(gx, 7); tree('E_Back1', 'Tree_Small', 32, 8.6); P('Props', 'Shed_E', SR + 'PROP_Route_GatePillar', 35.5, 7.2, {});
tree('D_Bush1', 'Bush', 23.6, 4.5); tree('D_Tree1', 'Tree_Small', 23.2, 8);
lamp('D1', 29.6, 6.6);                                                             // köşeden E'yi gösteren lamba
for (const [n, s, gx, gy] of [['DPuddle_1', 'Puddle_Deep', 27.5, 6], ['DPuddle_2', 'Puddle_M', 31, 4], ['DPuddle_3', 'Puddle_Deep', 33, 5.5]]) decal(n, s, gx, gy);
for (const [k, gy] of [['A', 2.1], ['B', 3.8], ['C', 5.4]]) P('LowRoad/Rubble', 'Rubble_' + k, 'N:PROP_FE_Rubble_' + k, 34.0, gy, { col: rectC(0.5, 0.8), h: 0 });
P('LowRoad/Rubble', 'Barrier_1', SR + 'PROP_Route_Barrier', 33.6, 3.0, { col: rectC(0.12, 0.5), h: 0 });
P('LowRoad/Rubble', 'Barrier_2', SR + 'PROP_Route_Barrier', 33.6, 5.6, { col: rectC(0.12, 0.5), h: 0 });
P('LowRoad/Rubble', 'Cone_1', SR + 'PROP_Route_Cone', 33.3, 2.3, { col: dia(0.12) });
P('LowRoad/Rubble', 'Cone_2', SR + 'PROP_Route_Cone', 33.3, 6.6, { col: dia(0.12) });
P('LowRoad/Rubble', 'Sign_Closed', 'N:PROP_FE_Sign_Closed', 33.5, 4.4, { col: dia(0.08) });
P('LowRoad/Rubble', 'DangerSign', SR + 'PROP_Route_DangerSign', 32.6, 6.7, { col: dia(0.08) });

// ================================================================ UZUN YOL: F (+gy) → G (+gx) → H (-gy) → I (+gx) → J (-gy)
// F
for (const gy of [19, 20.2, 21.4, 22.6]) fenceY(30, gy);
tree('F_Bush1', 'Bush', 23.6, 21); tree('F_Tree1', 'Tree_Pine', 21.5, 27.5); tree('F_Bush2', 'Bush', 23.8, 28.5);
lamp('F1', 29.6, 25.4);                                                            // köşe lambası
for (const [n, s, gx, gy] of [['FrPuddle_1', 'Puddle_S', 27, 21], ['FrDrain_1', 'Drain', 26.2, 24], ['FrPuddle_2', 'Puddle_M', 28, 28.5]]) decal(n, s, gx, gy);
// G
for (const gx of [25, 29.3, 33.6, 37.9, 42.2]) houseX(gx, 31);
[[2, 27], [3, 34], [1, 41]].forEach(([k, gx], i) => P('Buildings', 'Apt_G' + i, SU + 'PROP_StreetBG_Apt_' + k, gx, 38, { order: -70, h: 0 }));
P('Props', 'ParkedCar_G', SU + 'PROP_StreetBG_Car_B', 35.5, 26.9, { col: rectC(0.9, 0.35) });   // park etmiş araba: geçidi daraltır
lamp('G1', 39.6, 30.6);
// G-H-F-kavşak arasındaki bahçe (küçük meydancık görünümü, yürünmez)
tree('Yard_Tree2', 'Tree_Small', 39.8, 20.0); tree('Yard_Bench', 'Bench', 37, 24.6); tree('Yard_Bush1', 'Bush', 39.5, 24.3); tree('Yard_Swing', 'Swing', 35.5, 23.5);
for (const [n, s, gx, gy] of [['GPuddle_1', 'Puddle_L', 32, 28.2], ['GDrain_1', 'Drain', 38, 27.4], ['GPuddle_2', 'Puddle_S', 44, 28.5]]) decal(n, s, gx, gy);
// H
houseY(46, 18.2, 'Facade_A'); houseY(46, 22.5, 'Facade_Home');
lamp('H1', 45.6, 17.6);                                                            // köşe lambası (I'ye dönüş)
for (const [n, s, gx, gy] of [['HPuddle_1', 'Puddle_M', 43, 23], ['HDrain_1', 'Drain', 44.5, 19.5], ['HPuddle_2', 'Puddle_S', 42.5, 15.5]]) decal(n, s, gx, gy);
// I (ağaç olayı burada; ağaç + dal Eren/Arda prefabı, burada sadece boşluk ve çapalar)
for (const gx of [47.5, 51.8]) houseX(gx, 18);
tree('I_Bush1', 'Bush', 47, 11.4); tree('I_Bush2', 'Bush', 52, 11.5);
lamp('I1', 54.4, 17.6);
for (const [n, s, gx, gy] of [['IPuddle_1', 'Puddle_S', 48, 14.5], ['IPuddle_2', 'Puddle_M', 57, 16]]) decal(n, s, gx, gy);
houseX(56.1, 18, 'Facade_A');                                                        // I'nin sonu: köşeyi kapatan ev
// J: dar RÜZGÂR KORİDORU (gx 56..59). Ön tarafta alçak duvar + saksı çalıları, arkada evler; rüzgâr efekti çalışma anında (FE_WindCorridor)
houseY(59, 4.6, 'Facade_B'); houseY(59, 8.9, 'Facade_Home');
for (const gy of [1.5, 5.5, 9.5]) tree('J_Pot_' + gy, 'Bush', 55.2, gy);
for (const gy of [11.6, 6.2, 1.0]) lamp('J_' + gy, 58.6, gy);
for (const [n, s, gx, gy] of [['JPuddle_1', 'Puddle_M', 57.5, 10.2], ['JPuddle_2', 'Puddle_S', 57, 5.2], ['JPuddle_3', 'Puddle_M', 58, 1.6], ['JDrain_1', 'Drain', 56.6, 7.6], ['JDrain_2', 'Drain', 58.2, 3.4]]) decal(n, s, gx, gy);

// ================================================================ KORUNAKLI AVLU (devam noktası): yüksek taş duvar + bina girintisi + tente, rüzgâr yok
// ===== APARTMAN FİNALİ (final-world agent) =====
// Cenk'in apartmanı (59,-1): Facade_Home yerine giriş katı yeniden çizilmiş cephe (tools/sel_aksam_final/world/build_world_art.js;
// aynı boyut ve pivot, kapı boşluğu saydam). Kanat, hol, posta kutusu, lamba, paspas ve saksılar FE_ApartmentFinal prefabında,
// AptFacade çapasına göre (build_scene.cs'teki APARTMAN FİNALİ bloğu). Gövde aynı (Body_Home).
P('Buildings', 'Facade_Apt_59_-1', 'A:FloodEvening/Final/World/PROP_FE_Apt_Facade', 59, -1, { order: -50, h: 0 });
P('Buildings', 'Body_59_-1', SU + 'PROP_StreetBG_Body_Home', 59, -1, { order: -62, h: 0 });
// tente + komşular + su yığını girişin önünden avlunun sağ-ön köşesine alındı (kapıya ve posta kutusuna giden yol açık)
P('Shelter', 'Canopy', AS + 'PROP_Assembly_Canopy', 62.0, -6.9, { col: rectC(0.2, 0.2) });
for (const [n, s, gx, gy] of [['APuddle_1', 'Puddle_S', 58.8, -2.7], ['APuddle_2', 'Puddle_M', 62.0, -2.9], ['APuddle_3', 'Puddle_S', 56.6, -4.3]]) decal(n, s, gx, gy);
// ===== /APARTMAN FİNALİ =====
P('Shelter', 'Sign_SafeZone', 'N:PROP_FE_Sign_SafeZone', 54.6, -1.6, { col: rectC(0.45, 0.08) });
for (const [gx, gy] of [[53.8, -2.6], [62.4, -7.4], [56.4, -7.4]]) P('Shelter', 'Lamp_S_' + gx, AS + 'PROP_Assembly_Lamp', gx, gy, { col: dia(0.12), light: true });
P('Shelter', 'Bench_1', AS + 'PROP_Assembly_Bench', 55.6, -5.6, { col: rectC(0.5, 0.25) });
P('Shelter', 'Bench_2', AS + 'PROP_Assembly_Bench', 61.6, -5.4, { col: rectC(0.5, 0.25) });
// ===== APARTMAN FİNALİ (final-world agent): avlu düzeni =====
P('Shelter', 'Neighbor_A', AS + 'PROP_Assembly_Neighbor_A', 61.3, -7.4, { col: dia(0.15) });
P('Shelter', 'Neighbor_B', AS + 'PROP_Assembly_Neighbor_B', 62.3, -7.6, { col: dia(0.15) });
P('Shelter', 'WaterStack', AS + 'PROP_Assembly_WaterStack', 62.4, -4.4, { col: rectC(0.3, 0.3) });
// ===== /APARTMAN FİNALİ =====

// kanal / sel kenarı ağaçları
tree('Canal_Tree1', 'Tree_Small', 20, -2); tree('Canal_Tree2', 'Tree_Small', 24.5, 0.5);

const gates = [];
const anchors = {
  Spawn: [-7.5, 2.6], WarningSpot: [-5.5, 2.6], IntroView: [2, 4], Fork: [27, 14.5], LowRoad: [29, 4.5], HighRoad: [27.5, 21],
  TreeStopSpot: [47.2, 15.5], TreeFallSpot: [51, 15.5], BranchSpot: [51, 15.5],
  WindIntro: [57.5, 10.5], WindCorridor: [57.5, 6], WindEntry: [57.5, 12.6], WindExit: [57.5, -1.6],
  SafeZone: [58, -4.8], SafeSpot: [58, -4.6], 'SafeZone/StandPoint': [58, -4.8],
  AptFacade: [59, -1], AptDoor: [60.05, -1.42],               // APARTMAN FİNALİ (final-world agent): prefab kökü + kapı önü
};
const areas = {
  'LowRoad/BarrierZone': { box: true, from: [31.6, 2], to: [33.6, 7] },
  HighRoad: { poly: [[25, 20], [30, 20], [30, 23], [25, 23]], trigger: true },
  TreeStopSpot: { poly: [[46.2, 13], [48.2, 13], [48.2, 18], [46.2, 18]], trigger: true },
  WindIntro: { poly: [[56, 9.6], [59, 9.6], [59, 11.4], [56, 11.4]], trigger: true },
  WindCorridor: { poly: [[56, -1.3], [59, -1.3], [59, 12.8], [56, 12.8]], trigger: true },
  SafeZone: { poly: [[53.5, -7.6], [62.6, -7.6], [62.6, -2.2], [53.5, -2.2]], trigger: true },
};
module.exports = { objs, anchors, areas, gates };
