---

## File Reference Resolution Rule

If you are unsure about where a class or type is defined or referenced in the code (for example, in C#), always check the `using` statements at the top of the file. Then, search all files in the project to locate the definition and references of the class or type. You must only rely on what actually exists in the files—never assume the existence, structure, or members of any class, type, or file based on model knowledge or hallucination. All reasoning and actions must be strictly grounded in the real project files and their actual content. This ensures accurate context and prevents missing dependencies or incorrect assumptions about code structure.
