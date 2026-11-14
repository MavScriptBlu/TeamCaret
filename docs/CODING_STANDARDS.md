# Team Caret Coding Rules

## Code Style

- Use clear and descriptive names for variables, methods, classes, and files.
- Follow the coding conventions already established by the project.
- Keep methods focused on one main task.
- Avoid unnecessary or duplicated code.
- Keep code organized and easy for other team members to understand.

## Documentation

- Add comments when code is complex or requires additional explanation.
- Give every class and public method a `/// <summary>` that says what it is for in plain words. Say what it does, not how the language works.
- Put a short `//` comment above any step that isn't obvious, like a database rule, a workaround, or a "why".
- Skip comments on trivial properties and obvious boilerplate. A comment that repeats the code is noise.
- Use blank lines to split a method into logical blocks (check the input, do the work, return the result).
- Spell names out in full: `item`, not `i`, and `index`, not `idx`.
- Keep documentation up to date when making significant changes.
- Use meaningful commit messages that briefly explain what was changed.

## Testing and Error Handling

- Test new functionality before submitting it for review.
- Fix known bugs before adding unnecessary new features.
- Handle errors appropriately and avoid ignoring exceptions or errors.
- Do not commit code that is known to be broken unless the team has discussed it.

## Version Control

- Pull the latest changes before starting work.
- Commit changes regularly with clear commit messages.
- Do not commit directly to the main branch unless the team agrees it is necessary.
- Review code before merging changes into the main branch.
- Communicate with the team before making major changes that could affect someone else's work.
