import type { CommandItem } from './commandItem'

/**
 * Ranks the command palette's entries against what the operator has typed.
 *
 * Scoring is deliberately simple and predictable — a prefix beats a word start,
 * a word start beats a match anywhere. Nothing fuzzy: an operator typing "user"
 * expects "Benutzer" from its keywords, not a chain of coincidental letters.
 */
export function searchCommands(commands: readonly CommandItem[], query: string): CommandItem[] {
  const needle = query.trim().toLowerCase()
  if (!needle) {
    return [...commands]
  }

  return commands
    .map((command) => ({ command, score: scoreOf(command, needle) }))
    .filter((entry) => entry.score > 0)
    .sort((a, b) => b.score - a.score || a.command.label.localeCompare(b.command.label))
    .map((entry) => entry.command)
}

function scoreOf(command: CommandItem, needle: string): number {
  const haystacks = [command.label, command.section ?? '', ...(command.keywords ?? [])]

  let best = 0
  for (const [index, raw] of haystacks.entries()) {
    // The label is what the operator sees, so a hit there outranks a hit in a
    // keyword or the section name.
    const weight = index === 0 ? 3 : 1
    best = Math.max(best, matchScore(raw.toLowerCase(), needle) * weight)
  }
  return best
}

function matchScore(haystack: string, needle: string): number {
  if (!haystack) {
    return 0
  }
  if (haystack === needle) {
    return 10
  }
  if (haystack.startsWith(needle)) {
    return 6
  }
  // A match at a word boundary ("neu" in "Benutzer neu") reads as intentional.
  if (haystack.includes(` ${needle}`)) {
    return 4
  }
  return haystack.includes(needle) ? 2 : 0
}
