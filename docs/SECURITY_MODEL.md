# PHANTOMVAULT Security Model

## 1. Security Guarantees

PHANTOMVAULT provides cryptographic assurances based on standard, vetted algorithms:

1. **Confidentiality at Rest**:
   - Plaintext file contents and metadata are encrypted with AES-256 in Galois/Counter Mode (GCM).
   - Filenames, directories, sizes, and timestamps are stored within an encrypted SQLite catalog inside the vault.
2. **Authenticity & Integrity**:
   - Every file chunk is authenticated using a 128-bit authentication tag.
   - Any bit flip, truncation, or chunk reordering attempt causes decryption to immediately fail, throwing an error without outputting partial corrupted plaintext.
3. **Key Isolation**:
   - The master Data Encryption Key (DEK) is generated via `RandomNumberGenerator.GetBytes(32)`.
   - The user password is never used directly to encrypt data. It derives a Key Encryption Key (KEK) that wraps the DEK.
   - Changing a vault password requires re-encrypting only the 32-byte DEK, eliminating the need to re-encrypt gigabytes of stored data while providing cryptographic safety.
4. **Resilience to Offline Attacks**:
   - Derivation employs Argon2id with 64 MiB memory cost, 4 iterations, and 4 degrees of parallelism. This forces attackers attempting offline brute-force attacks to expend massive RAM and ASIC resources per candidate password.
5. **Nonce Safety Guarantee**:
   - AES-GCM requires that a (key, nonce) pair is never repeated.
   - PHANTOMVAULT enforces a deterministic hierarchy: for each file, a unique 64-bit random IV prefix is chosen, combined with a 32-bit chunk counter. A single key can safely encrypt billions of files and chunks without nonce collision risk.

---

## 2. Security Boundaries

| Zone | Boundary Description | Security Measures |
| :--- | :--- | :--- |
| **Storage at Rest** | Disk filesystem, external drives, backups | AES-256-GCM encrypted chunks, encrypted SQLite database, authenticated headers. |
| **In-Memory Active** | Active user process when unlocked | DEK stored in pinned `SecureBuffer`. Fast zero-fill on lock or timeout. UI models only store metadata for the currently active view. |
| **Steganographic Carrier** | Public PNG or WAV files | AES-256-GCM payload embedded in lowest significant bits with HMAC-authenticated container headers. |
| **System Interaction** | Windows session lock, sleep, hibernate | Event listener triggers immediate vault locking before screen lock. |

---

## 3. Residual Risks & Transparent Disclaimers

1. **No Defense Against Active OS Kernel Compromise**:
   - If an adversary operates a kernel rootkit or administrative-level spyware on the host machine, they can inspect process memory or inject DLLs. PHANTOMVAULT operates within standard user space and cannot defend against a compromised operating system.
2. **Side-Channel & Paging Leakage**:
   - The .NET Garbage Collector moves memory. While `SecureBuffer` uses pinned unmanaged memory and calls `CryptographicOperations.ZeroMemory` upon disposal, transient memory copies during GCM block operations may temporarily exist in volatile RAM until garbage collected.
3. **Statistical Steganalysis**:
   - LSB steganography modifies the least-significant bit parity of carrier pixels or audio samples. While the payload is strongly encrypted and pseudorandom, statistical steganalysis tools (e.g., Chi-square, sample-pair analysis) can identify anomalies compared to pristine natural photographs. The steganography feature is designed for discreet transport, not absolute mathematical indistinguishability against state-sponsored forensic laboratories.
