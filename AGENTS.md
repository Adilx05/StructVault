# StructVault Development Rules

## 🧠 Core Philosophy

StructVault is:
- Structure-first
- Security-first
- File-based

NOT:
- A simple password manager
- A cloud system

---

## 🔐 Security Rules (STRICT)

- NEVER store plaintext sensitive data on disk
- ALL vault data MUST be encrypted
- Use Argon2 for key derivation
- Use AES-256-GCM for encryption
- Password MUST be required (non-empty)

---

## 🗄️ Storage Rules

- ONLY SQLite inside encrypted container
- DO NOT use JSON as primary storage
- DO NOT expose raw DB

---

## 🧩 Architecture Rules

- MVVM pattern required
- Keep separation clean but pragmatic
- Avoid over-engineering

---

## 🖥️ UI Rules

- Use MahApps.Metro
- Keep UX simple
- Prioritize usability over complexity

---

## ⚙️ Feature Rules

- No autosave
- Manual save with dirty tracking
- Clipboard auto-clear
- Idle lock configurable

---

## ❌ Forbidden

- No cloud sync
- No multi-user
- No unnecessary abstraction layers
- No insecure shortcuts

---

## 🎯 Goal

Build a fast, secure, offline-first structured vault system.
