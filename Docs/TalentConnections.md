# 天赋连线绑定

灰色表示未打通；两端解锁后变为黄色。根天赋按现有解锁规则视为默认可用。
共用线段按所列任一路线激活；选中图钉与路线状态独立。
节点层级：TalentTreePanel/Nodes/Connections。每段可在 Inspector 修改 Locked Color / Unlocked Color。

保持原图的额外视觉连接 1—2、10—13，不修改天赋解锁依赖。
独立导出缺少的 8 段从 All.png 复用；13—14 的短横线复用现有横线纹理补齐。
使用 RawImage 的 UVRect 选取纹理区域，保留原 PNG，不另存裁切图片。

| 场景对象 | 对应端点（任一组打通即亮） | 原素材 | 区域 X,Y,右,下 |
|---|---|---|---|
| Part00 | Talent0 ↔ Talent3 | 023715d772bd5059bc0cbbc1100cb83c.png | [953, 939, 961, 980] |
| Part01 | Talent5 ↔ Talent32 | 03f34af2199d4465293e596dd322929b.png | [479, 1010, 689, 1030] |
| Part02 | Talent6 ↔ Talent2 | 045a6e47f9349f91727edc7319dcd659.png | [782, 867, 848, 932] |
| Part03 | Talent0 ↔ Talent2 | 045a6e47f9349f91727edc7319dcd659.png | [885, 962, 925, 1000] |
| Part04 | Talent5 ↔ Talent26 | 07bc9d485d7c7fe1799d17fcee7154c8.png | [614, 885, 634, 928] |
| Part05 | Talent12 ↔ Talent23 | 0c522c3145dc92109ae979124f9cac78.png | [780, 255, 949, 284] |
| Part06 | Talent16 ↔ Talent8 | 19d6e01d5ce0aef66fd5dd603ddfe522.png | [1196, 747, 1302, 892] |
| Part07 | Talent1 ↔ Talent5 | 1cf710408fc44fce628fa08f716e7215.png | [744, 1011, 808, 1027] |
| Part08 | Talent0 ↔ Talent1 | 1cf710408fc44fce628fa08f716e7215.png | [864, 1013, 917, 1031] |
| Part09 | Talent9 ↔ Talent37 | 3fbf7dfd51e6a4630a35754f66ee7984.png | [1187, 949, 1209, 996] |
| Part10 | Talent7 ↔ Talent13 | 429034fe4712edba6a24d2ce9cba73b4.png | [952, 696, 973, 752] |
| Part11 | Talent3 ↔ Talent7 | 429034fe4712edba6a24d2ce9cba73b4.png | [946, 804, 965, 871] |
| Part12 | Talent28 ↔ Talent27 | 5bba2a0ed95c9da91528ef994bc69f5f.png | [328, 657, 494, 757] |
| Part13 | Talent29 ↔ Talent5 | 5ce0d6cdbb81142eb5bc0bdc4607c95d.png | [481, 875, 620, 930] |
| Part14 | Talent29 ↔ Talent5, Talent5 ↔ Talent26 | 5ce0d6cdbb81142eb5bc0bdc4607c95d.png | [620, 921, 728, 961] |
| Part15 | Talent35 ↔ Talent36 | 6ae6b4637eb031d897b28dd9a0897631.png | [1451, 849, 1552, 886] |
| Part16 | Talent10 ↔ Talent15 | 6b3728ea53f6be7e49c95c7a736f2d17.png | [975, 371, 1105, 397] |
| Part17 | Talent0 ↔ Talent4, Talent0 ↔ Talent9 | 6dcc19408c40ecdbc51e5b4076e0a38b.png | [1002, 1013, 1088, 1030] |
| Part18 | Talent0 ↔ Talent9 | 6dcc19408c40ecdbc51e5b4076e0a38b.png | [1088, 1014, 1176, 1031] |
| Part19 | Talent22 ↔ Talent23 | 7686f3cedc792f5b7645dda26f371869.png | [578, 396, 775, 504] |
| Part20 | Talent20 ↔ Talent25 | 820ee6f29d827e862b7fe0dca519b801.png | [770, 677, 829, 718] |
| Part21 | Talent25 ↔ Talent26 | 820ee6f29d827e862b7fe0dca519b801.png | [666, 745, 730, 813] |
| Part22 | Talent31 ↔ Talent30 | 84f1638f077dd92055ef5cc0d38e5516.png | [113, 775, 312, 844] |
| Part23 | Talent29 ↔ Talent31 | 84f1638f077dd92055ef5cc0d38e5516.png | [361, 844, 430, 881] |
| Part24 | Talent33 ↔ Talent36 | 87a278c07256980735eeaa57f63f5e99.png | [1530, 729, 1572, 819] |
| Part25 | Talent7 ↔ Talent20 | 8e17f4d7432352e657f0267d17ebae8f.png | [880, 773, 934, 795] |
| Part26 | Talent36 ↔ Talent38 | 96f91ff97c79c7440914bc14ca25e50f.png | [1604, 774, 1804, 848] |
| Part27 | Talent16 ↔ Talent8 | a96488d90c08f1998691536376fdc5fe.png | [1193, 841, 1275, 895] |
| Part28 | Talent4 ↔ Talent8 | a96488d90c08f1998691536376fdc5fe.png | [1087, 908, 1153, 953] |
| Part29 | Talent26 ↔ Talent6 | aaecc39e10f34403da87b3b0e1628353.png | [743, 854, 777, 908] |
| Part30 | Talent29 ↔ Talent5, Talent5 ↔ Talent26 | aaecc39e10f34403da87b3b0e1628353.png | [704, 951, 731, 1010] |
| Part31 | Talent15 ↔ Talent17 | ad7d3b5994f898868bebcdd45f5cfc74.png | [1187, 414, 1321, 487] |
| Part32 | Talent12 ↔ Talent11 | ba4ca08bad46c80e1286ed9f7b669619.png | [955, 139, 970, 236] |
| Part33 | Talent10 ↔ Talent12 | ba4ca08bad46c80e1286ed9f7b669619.png | [954, 290, 971, 345] |
| Part34 | Talent22 ↔ Talent23 | baf7afef65e44538781c26494e338eec.png | [576, 495, 641, 581] |
| Part35 | Talent9 ↔ Talent35 | c4df0c2b894922e655146401775bc590.png | [1228, 1012, 1445, 1032] |
| Part36 | Talent9 ↔ Talent37 | ce2a6f817aed0ab64b24804db37272ce.png | [1192, 931, 1262, 956] |
| Part37 | Talent17 ↔ Talent19 | d7a6b19626ef0e0b7130351503b0b37e.png | [1359, 516, 1420, 568] |
| Part38 | Talent19 ↔ Talent33 | d7a6b19626ef0e0b7130351503b0b37e.png | [1469, 629, 1508, 687] |
| Part39 | Talent10 ↔ Talent13 | da81d45398ef0f620b7862340b92adb3.png | [952, 414, 969, 641] |
| Part40 | Talent22 ↔ Talent21 | e1e09f12341e626b3c2a226cf457da7a.png | [686, 535, 787, 591] |
| Part41 | Talent26 ↔ Talent28 | e82a14e1279826ac90c0f5f4da22c613.png | [532, 773, 619, 831] |
| Part42 | Talent26 ↔ Talent6 | e82a14e1279826ac90c0f5f4da22c613.png | [686, 864, 750, 906] |
| Part43 | Talent35 ↔ Talent36 | f6ccd6779679e17331c11b3095b642a4.png | [1446, 880, 1479, 994] |
| Part44 | Talent3 ↔ Talent2 | fb9ea0c20d65f2d904a07240bc1d4e3e.png | [886, 890, 925, 923] |
| Part45 | Talent3 ↔ Talent4 | fb9ea0c20d65f2d904a07240bc1d4e3e.png | [995, 894, 1056, 944] |
| Part46 | Talent1 ↔ Talent2 | fb9ea0c20d65f2d904a07240bc1d4e3e.png | [835, 960, 857, 995] |
| Part47 | Talent0 ↔ Talent4 | fb9ea0c20d65f2d904a07240bc1d4e3e.png | [1078, 988, 1089, 1028] |
| Part48 | Talent23 ↔ Talent24 | All.png | [703, 180, 736, 254] |
| Part49 | Talent17 ↔ Talent18 | All.png | [1353, 315, 1479, 482] |
| Part50 | Talent22 ↔ Talent23 | All.png | [743, 333, 775, 402] |
| Part51 | Talent14 ↔ Talent15 | All.png | [1066, 439, 1145, 658] |
| Part52 | Talent17 ↔ Talent16 | All.png | [1184, 519, 1334, 722] |
| Part53 | Talent21 ↔ Talent20 | All.png | [810, 554, 853, 656] |
| Part54 | Talent33 ↔ Talent34 | All.png | [1544, 579, 1731, 697] |
| Part55 | Talent7 ↔ Talent20 | All.png | [857, 702, 894, 792] |
| Part56 | Talent13 ↔ Talent14 | 03f34af2199d4465293e596dd322929b.png | [479, 1010, 689, 1030] |
