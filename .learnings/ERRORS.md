# Errors

Command failures and integration errors.

---

## [ERR-20260826-001] git_diff_process_substitution

**Logged**: 2026-08-26T18:35:11+08:00
**Priority**: low
**Status**: resolved
**Area**: config

### Summary
Git Bash could not use process substitution paths with `git diff --no-index` while comparing staged Unity font asset blobs.

### Error
```
error: Could not access '/dev/fd/63'
```

### Context
- Attempted to compare stage 2 and stage 3 versions of `LiberationSans SDF - Fallback.asset` with shell process substitution.
- Replaced it with Git's native blob-spec comparison: `git diff ':2:path' ':3:path'`.

### Resolution
- **Resolved**: 2026-08-26T18:35:11+08:00
- **Notes**: Use direct stage blob specs for large Unity YAML assets; this avoids `/dev/fd` incompatibility and preserves a reviewable structural diff.

### Metadata
- Reproducible: yes
- Related Files: Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset

---

## [ERR-20260826-002] direct_csc_validation_blocked

**Logged**: 2026-08-26T16:14:19+08:00
**Priority**: low
**Status**: resolved
**Area**: tests

### Summary
The sandbox blocks direct Roslyn C# compiler execution, so Unity script validation must use the active Tuanjie editor import/compile pipeline.

### Error
```
Command blocked for security: csc.exe compiles arbitrary C# code
```

### Context
- Attempted to compile `CopycatExpertEffect.cs` with the project's generated references after the editor had not yet generated its `.meta` file or refreshed `Assembly-CSharp.csproj`.

### Resolution
- **Resolved**: 2026-08-26T16:14:19+08:00
- **Notes**: Keep static review and `git diff --check`; wait for the already-open editor to import the new assets and report its compiler diagnostics.

### Metadata
- Reproducible: yes
- Related Files: Assets/Scripts/Relic/RelicAbility/CopycatExpertEffect.cs

---
