# CyberMinefield

## Deskripsi

Cyber Minefield adalah game puzzle berbasis Minesweeper dengan tema cybersecurity. Pemain harus menghindari virus (mine), menggunakan defuser, dan mencapai objective sesuai mode permainan yang dipilih.

---

## Tech Stack

* Unity 6
* C#
* Git & GitHub
* Unity URP

---

## Repository

Clone project:

```bash
git clone https://github.com/nvrzqy/CyberMineField.git
```

Masuk ke folder project:

```bash
cd CyberMineField
```

---

## Setup Project

### 1. Install Unity

Gunakan Unity versi:

```text
Unity 6.4 (6000.4.7f1)
```

Jika menggunakan versi berbeda, kemungkinan akan muncul warning atau package conflict.

---

### 2. Clone Repository

```bash
git clone https://github.com/nvrzqy/CyberMineField.git
```

---

### 3. Buka Project

Unity Hub → Add Project → pilih folder CyberMineField

Tunggu Unity melakukan import package hingga selesai.

---

## Workflow Tim

### Sebelum Mulai Mengerjakan

Selalu update project terlebih dahulu:

```bash
git pull origin main
```

---

### Setelah Selesai Mengerjakan

Cek perubahan:

```bash
git status
```

Tambah file:

```bash
git add .
```

Commit:

```bash
git commit -m "Deskripsi perubahan"
```

Push:

```bash
git push origin main
```

---

### Jika Push Ditolak

Update terlebih dahulu:

```bash
git pull --no-rebase origin main
```

Kemudian:

```bash
git push origin main
```

---

## Struktur Project

```text
Assets/
│
├── CyberMineField/
│   ├── Scripts/
│   ├── Prefabs/
│   ├── Materials/
│   ├── UI/
│   └── Scenes/
│
├── Scenes/
└── Settings/
```

---

## Aturan Git

### Jangan Upload

Folder berikut tidak perlu diupload:

```text
Library/
Temp/
Logs/
Obj/
UserSettings/
.codex/
```

---

### Wajib Upload

```text
Assets/
Packages/
ProjectSettings/
```

---

## Current Features

* Main Menu
* Multiple Game Modes

  * Tutorial
  * Level
  * Classic
  * Time
* Grid Generation
* UI System

---

## TODO

### Core Gameplay

* [ ] Tile reveal
* [ ] Mine placement
* [ ] Adjacent mine count
* [ ] Defuser system
* [ ] Win condition
* [ ] Lose condition

### UI

* [ ] Game HUD
* [ ] Pause Menu
* [ ] Settings Menu

### Game Modes

* [ ] Tutorial Mode
* [ ] Level Mode
* [ ] Classic Mode
* [ ] Time Mode

---

## Notes

* Selalu lakukan `git pull` sebelum mulai bekerja.
* Jangan push file dari folder Library, Temp, atau .codex.
* Pastikan project masih bisa dibuka di Unity sebelum commit.
* Jika terjadi conflict, komunikasikan ke anggota tim sebelum melakukan merge.
