# ⏱️ Unity Time Tracker

A lightweight time tracking tool for Unity, designed especially for **self-employed developers** who want to measure real working time without friction.

Tracks your work sessions automatically and stores them in a simple JSON file for easy access, analysis, or integration with other tools.

<img width="33%" height="900" alt="image" src="https://github.com/user-attachments/assets/14ef705d-b0ce-491a-a5ac-397cfb8a8989" />
<img width="33%" height="900" alt="image" src="https://github.com/user-attachments/assets/342da359-5e86-45a8-8bd6-2ef200d68e57" />
<img width="33%" height="900" alt="image" src="https://github.com/user-attachments/assets/649e3b4c-1e83-46b0-9d13-9d450198ee36" />

---

## 🛠️ Installation & Setup

Git
```bash
git clone https://github.com/EthanGrane/Unity-Time-Tracker.git
```

Git URL (Unity Package Manager)
```bash
https://github.com/EthanGrane/Unity-Time-Tracker.git
```

Follow these steps to get started:

### 1. Install
Download or clone this repository into your Unity project.

### 2. Move to Editor folder
Place the `UnityTimerTracker` folder inside: ```Assets/Editor/```

### 3. Open the window
Tools -> Time Tracker

<img width="186" height="110" alt="image" src="https://github.com/user-attachments/assets/e9298924-fc81-4aa7-b6bd-bf5aa72e1f47" />



---

## 🔗 GitHub Setup Guide

You can link your repository to display commits in the timeline.

1. Create a Personal Access Token

Go to GitHub:

Settings → Developer settings → Personal access tokens
Click "Generate new token"

Make sure to enable:

✅ repo (required for private repositories)

---

## 📂 Troubleshooting

You need to put the UnityTimerTracker folder in the path ```Assets/Editor/```

---

## 📂 How It Works

Each work session is stored as:

```json
{
  "start": "2026-03-29 10:15:23",
  "lastCheck": "2026-03-29 11:02:10"
}
```

---

## ⚠️ Disclaimer ⚠️
I'm not a fan of the "vibecoding" approach, this tool was generated with AI (Claude) based on my code and design style. 
