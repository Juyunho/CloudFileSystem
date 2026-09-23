# Command Based Undo and Redo

## Context

The prior handler cloned the entire tree before each delete, paste, or tag edit. It was reliable for a small sample but coupled mutation and history, used memory proportional to the full tree, and did not implement Command despite the UI terminology.

## Decision

Use `IFileSystemCommand` with `Execute` and `Undo`. `DeleteNodeCommand` stores the removed node, parent, and index. `SetTagsCommand` stores old and new tag values. `PasteNodeCommand` stores the target and copied subtree. `FileSystemCommandHistory` owns undo and redo stacks; executing a new command clears redo history.

Copy remains a frontend clipboard action. Paste is the server mutation and history entry.

## Alternatives Considered

- Keep whole-tree snapshots as a Memento-style implementation.
- Store event records and rebuild the tree by replay.
- Add one generic command with delegates.

## Consequences

Each mutation retains only the state required for reversal, responsibilities are explicit, and commands are unit testable. Commands currently operate on one in-memory aggregate under a handler lock; persistent transactions and multi-user concurrency would need a different boundary.
