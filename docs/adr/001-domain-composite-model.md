# Domain Composite Model

## Context

The original runtime tree used one transport-shaped `FileSystemNode` with a node type discriminator and nullable metadata for every file subtype. Recursion worked, but the model could represent invalid combinations and the UML overstated the inheritance present in executable code.

## Decision

Use `FileSystemNode` as the abstract component, `DirectoryNode` as the composite, and `FileNode` as the abstract leaf base. `WordFileNode`, `ImageFileNode`, and `TextFileNode` own only their valid metadata. A directory owns its children and maintains each child's parent relationship. API DTOs remain separate to preserve the existing Angular contract.

## Alternatives Considered

- Keep one flat node type and improve validation.
- Return polymorphic domain objects directly from the API.
- Put all traversal operations on every node.

## Consequences

Recursive structure and total size are type safe, and the UML maps to real classes. DTO mapping adds a small amount of boundary code. Search, XML, and logging remain application operations so transport and presentation concerns do not enter the domain.
