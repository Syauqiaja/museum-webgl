# Aset Game Permainan Tradisional Nusantara

Dibuat 2026-08-03. Semua FBX: skala meter, Y-up, tekstur ter-embed, siap import Unity.

---

## 1_Karakter — 4 karakter chibi ter-rig

| File | Karakter | Kostum |
|---|---|---|
| `Char_Jawa_L.fbx` | Laki-laki, Jawa | blangkon, surjan lurik, celana batik parang |
| `Char_Bali_P.fbx` | Perempuan, Bali | kebaya renda, selendang emas, celana motif Bali, kamboja |
| `Char_Minang_L.fbx` | Laki-laki, Sumatra Barat | deta bersudut, taluak balango, celana songket merah |
| `Char_Bugis_P.fbx` | Perempuan, Sulawesi Selatan | baju bodo oranye, celana tenun hijau, sanggul |

- Tinggi 1,20 m · ~20.500 tris · 1 material + 1 tekstur per karakter
- Kerangka **24 tulang penamaan Mixamo** (`Hips`, `LeftUpLeg`, `Spine`, `Head`, …)
- Semua memakai **celana**, bukan rok — supaya kaki tidak tembus saat animasi engklek & egrang

## 2_Animasi — 7 klip

| File | Frame | Sumber |
|---|---|---|
| `Anim_Walk.fbx` | — | Meshy (gratis dari rigging) |
| `Anim_Run.fbx` | — | Meshy |
| `Anim_Idle.fbx` | 1–60 | Blender, buatan tangan |
| `Anim_Egrang.fbx` | 1–60 | Blender |
| `Anim_Engklek.fbx` | 1–32 | Blender |
| `Anim_Idle_Egrang.fbx` | 1–60 | Blender |
| `Anim_Idle_Engklek.fbx` | 1–60 | Blender |

Karena keempat karakter memakai kerangka yang sama, **satu klip cukup untuk semua** lewat retarget Humanoid Unity.

## 3_Egrang_Anak — pasangan karakter chibi

Tinggi tiang 140 cm · pijakan 25 cm · jarak batang 30 cm.
Terverifikasi pas: tangan karakter ±0,148 vs penanda `Grip` ±0,150 (meleset 2 mm).

## 4_Egrang_Dewasa — versi skala manusia dewasa

Tinggi tiang 200 cm · pijakan 40 cm · jarak batang 48 cm.

### Luas penampang (WAJIB dipertahankan — ini materi ajarnya)

| Varian | Ukuran | Luas terukur | Target | Kesulitan |
|---|---|---|---|---|
| Persegi | sisi 8,00 cm | **64,0000 cm²** | 64,00 | Mudah |
| Lingkaran | ⌀ 8,00 cm | **50,2399 cm²** | 50,24 | Sedang |
| Segitiga sama kaki | alas 8 × tinggi 8 cm | **32,0000 cm²** | 32,00 | Sulit |

Penampang 8 cm **sama pada versi anak maupun dewasa** — yang berubah hanya tinggi, jarak, dan posisi pijakan.

**Cara cek ulang setelah mengedit mesh:** iris horizontal di ujung atas tiang, ambil convex hull titik-titiknya, hitung luasnya. Harus tetap 64 / 50,24 / 32 cm².

### Penanda di tiap egrang
- `_Tip` — titik tumpu tanah (pivot untuk miring/jatuh)
- `_Foot` — permukaan pijakan kaki
- `_Grip` — titik pegangan tangan

---

## Cara import ke Unity

1. Salin `1_Karakter`, `2_Animasi`, `3_Egrang_Anak` ke `Assets/`
2. Tiap `Char_*.fbx` → tab **Rig** → Animation Type **Humanoid**, Avatar Definition *Create From This Model*
3. Tiap `Anim_*.fbx` → **Rig → Humanoid**, Avatar Definition *Copy From Other Avatar* → pilih avatar karakternya
4. Tab **Animation** → centang **Loop Time** untuk semua klip
5. Egrang: tab **Materials** → *Extract Textures* lalu *Extract Materials*
6. Project memakai **URP** → material bawaan akan pink, konversi lewat *Edit ▸ Rendering ▸ Materials ▸ Convert Selected*

---

## Batasan yang perlu diketahui

- Animasi egrang, engklek, dan idle adalah **keyframe sederhana** (4–5 pose per siklus), bukan motion capture. Terbaca jelas untuk prototipe, tapi belum ada detail seperti pergeseran berat badan halus atau antisipasi gerak.
- Karakter Minang lengannya sedikit lebih rendah dari tiga lainnya sejak hasil Meshy (bukan T-pose sempurna). Retarget Humanoid semestinya mengoreksi ini — layak dicek di Unity.
- Tiang 8 cm pada egrang anak (140 cm) terlihat lebih gempal dibanding versi dewasa. Ini konsekuensi tak terhindarkan dari mengunci penampang di 8 cm.

## _Sumber
File `.blend`, tekstur PNG lepasan, dan gambar referensi desain karakter.
