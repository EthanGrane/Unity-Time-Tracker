# ⏱️ Unity Time Tracker

A lightweight time tracking tool for Unity, designed especially for **self-employed developers** who want to measure real working time without friction.

Tracks your work sessions automatically and stores them in a simple JSON file for easy access, analysis, or integration with other tools.

<img width="45%" height="auto" alt="image" src="https://github.com/user-attachments/assets/a2be1d95-4a97-48df-b3c1-5f435ae6c19f" />
<img width="45%" height="auto" alt="image" src="https://github.com/user-attachments/assets/d191701b-158b-44f8-a6cd-114fd0985711" />


---

## 🛠️ Installation & Setup

Follow these steps to get started:

### 1. Install
Download or clone this repository into your Unity project.

### 2. Move to Editor folder
Place the `UnityTimerTracker` folder inside: ```Assets/Editor/(Folder here)```

---

## 🚀 Features

- 🕒 Automatic session tracking using timestamps  
- 📅 Daily, weekly, and monthly stats  
- 📊 Work session analytics:
  - Total time worked  
  - Average time per day  
  - Average time per active day  
  - Longest / shortest session  
- 📁 Simple JSON storage (easy to read & modify)  
- ⚡ Lightweight and easy to integrate into any Unity project  

---

## 📂 Why isn't it working?

You need to put the UnityTimerTracker folder in the path ```Assets/Editor/(Folder here)```

---

## 📂 How It Works

Each work session is stored as:

```json
{
  "start": "2026-03-29 10:15:23",
  "lastCheck": "2026-03-29 11:02:10"
}
```
