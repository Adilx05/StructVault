# StructVault Tasks

---

## 🧱 Phase 1 – Solution Setup
- [x] Create solution with layered architecture:
  - Domain
  - Application
  - Infrastructure
  - Persistence
  - Desktop (WPF)
- [x] Setup WPF project with MahApps.Metro
- [x] Implement MVVM base:
  - ViewModelBase (INotifyPropertyChanged)
  - RelayCommand / AsyncCommand

---

## 🔐 Phase 2 – Security Core

### Implementation
- [x] Implement Argon2id key derivation service
- [x] Implement AES-256-GCM encryption service
- [x] Design QPS file format (header + salt + iv + encrypted data)

### Tests
- [x] Test: same password → same key (with same salt)
- [x] Test: different password → different key
- [x] Test: encryption → decryption returns original data
- [x] Test: tampered data → decryption fails

---

## 📦 Phase 3 – QPS File System

### Implementation
- [x] Implement QPS file writer
- [x] Implement QPS file reader
- [x] Handle header parsing
- [x] Handle versioning support

### Tests
- [x] Test: create vault → file is generated
- [x] Test: open vault → data loads correctly
- [x] Test: invalid password → open fails
- [x] Test: corrupted file → handled gracefully

---

## 🗄️ Phase 4 – Persistence Layer

### Implementation
- [x] Define SQLite schema (VaultNode, VaultField)
- [x] Setup in-memory SQLite lifecycle
- [x] Implement DB serialization/deserialization

### Tests
- [x] Test: create node → stored correctly
- [x] Test: create field → linked to node
- [x] Test: delete node → cascade delete fields
- [x] Test: ordering works as expected

---

## 🌲 Phase 5 – Core Domain Logic

### Implementation
- [x] Node CRUD operations
- [x] Field CRUD operations
- [x] Node hierarchy (parent-child)
- [x] Field ordering logic

### Tests
- [x] Test: nested node structure builds correctly
- [x] Test: node rename works
- [x] Test: duplicate field keys allowed
- [x] Test: field updates persist

---

## 🖥️ Phase 6 – UI Core (WPF + MahApps)

### Implementation
- [x] MainWindow layout (Tree + Detail panel)
- [x] Bind TreeView to node structure
- [x] Implement dynamic field rendering
- [x] Context menus for Node and Field

### Tests (UI / ViewModel level)
- [x] Test: selecting node updates detail panel
- [x] Test: adding node updates tree
- [x] Test: adding field updates UI model

---

## 💾 Phase 7 – Save / Load Workflow

### Implementation
- [x] Implement manual save system
- [x] Implement dirty flag tracking
- [x] Prompt user on exit if unsaved changes exist
- [x] Create `.bak` backup before overwrite

### Tests
- [x] Test: dirty flag triggers correctly
- [x] Test: save clears dirty state
- [x] Test: backup file is created
- [x] Test: restore from backup works

---

## 🔍 Phase 8 – Search Feature

### Implementation
- [x] Implement global search (nodes + fields)
- [x] Add filtering support

### Tests
- [x] Test: search by node name
- [x] Test: search by field value
- [x] Test: partial match works

---

## 📋 Phase 9 – Clipboard Security

### Implementation
- [x] Copy field value to clipboard
- [x] Auto-clear clipboard after configurable time

### Tests
- [x] Test: value copied correctly
- [x] Test: clipboard cleared after timeout
- [x] Test: disable auto-clear works

---

## 🔒 Phase 10 – Idle Lock

### Implementation
- [x] Track user inactivity
- [x] Lock vault after timeout
- [x] Prompt for password to unlock

### Tests
- [x] Test: idle timer triggers lock
- [x] Test: correct password unlocks
- [x] Test: incorrect password fails

---

## ⚙️ Phase 11 – Settings

### Implementation
- [x] Clipboard settings (enable/disable + duration)
- [x] Idle lock settings (enable/disable + duration)

### Tests
- [x] Test: settings persist correctly
- [x] Test: settings applied at runtime

---

## 🔐 Phase 12 – Password Management

### Implementation
- [x] Change master password
- [x] Re-encrypt vault with new password

### Tests
- [x] Test: old password no longer works
- [x] Test: new password opens vault
- [x] Test: data integrity preserved

---

## 🚀 Phase 13 – Final Polish

- [x] Improve UI responsiveness
- [x] Improve error handling
- [x] Add loading states
- [x] Add basic logging

---

## 🧪 Optional Enhancements

- [x] Drag & drop node ordering
- [x] Field reordering (drag & drop)
- [ ] Theme switching
