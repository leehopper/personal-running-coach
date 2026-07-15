import type { ReactElement } from 'react'

// Retained, unmounted-by-transcript as of Slice 3 PR-B: `coach-chat.
// component.tsx`'s transcript now renders `UserTurn`/`CoachTextTurn`
// instead of `MessageBubble`/`ChatBubble`. This component (and its
// content-block filter) stays because it is the documented Slice-4
// tool-use content-block contract — kept intentionally, not dead code.

// Anthropic content-block discriminator. The chat surface only renders
// `text` blocks in Slice 1; `thinking`, `tool_use`, `tool_result`, and
// `signature` blocks pass through opaquely (never rendered as text) so the
// component contract stays stable when Slice 4 turns on tool use.
export type MessageContentBlock =
  | { type: 'text'; text: string }
  | { type: 'thinking'; thinking: string }
  | { type: 'tool_use'; id: string; name: string; input: unknown }
  | { type: 'tool_result'; tool_use_id: string; content: unknown }
  | { type: 'signature'; signature: string }

export type MessageRole = 'user' | 'assistant'

export interface MessageBubbleProps {
  role: MessageRole
  content: readonly MessageContentBlock[]
  pending?: boolean
}

const isTextBlock = (block: MessageContentBlock): block is { type: 'text'; text: string } =>
  block.type === 'text'

// djb2 hash → reasonably collision-resistant for short text blocks and
// stable across renders (used only as a React key suffix, never for
// security or persistence).
const hashText = (text: string): number => {
  let hash = 5381
  for (let index = 0; index < text.length; index += 1) {
    hash = (hash * 33) ^ text.charCodeAt(index)
  }
  return hash >>> 0
}

// Role-based styling lives in a static map so consumers can tell at a
// glance which classes attach to which side of the conversation.
// Semantic tokens only — the user bubble uses the `primary` accent, the
// assistant bubble the neutral `muted` surface. No shadcn primitive is
// needed at this size.
const ROLE_STYLES: Record<MessageRole, string> = {
  user: 'ml-auto bg-primary text-primary-foreground rounded-2xl rounded-br-sm',
  assistant: 'mr-auto bg-muted text-foreground rounded-2xl rounded-bl-sm',
}

export const MessageBubble = ({
  role,
  content,
  pending = false,
}: MessageBubbleProps): ReactElement | null => {
  // Ignore non-text blocks per spec § Unit 3 R03.6 — `thinking` / `tool_use`
  // / `tool_result` / `signature` must never be rendered as text. This
  // filter is the single guard for that contract.
  const textBlocks = content.filter(isTextBlock)

  // Nothing renderable → omit the bubble shell entirely so a
  // thinking-/tool-only assistant turn doesn't leave a blank box in the
  // transcript.
  if (textBlocks.length === 0) {
    return null
  }

  return (
    <div
      data-testid="message-bubble"
      data-role={role}
      data-pending={pending ? 'true' : 'false'}
      className={`max-w-[80%] px-4 py-2 text-sm ${ROLE_STYLES[role]} ${
        pending ? 'opacity-60' : ''
      }`}
    >
      {textBlocks.map((block, index) => (
        <p
          // Content-derived key: positional index is reinforced by a hash
          // of the block's text so that React reuses the right `<p>` even
          // if a future producer re-orders or inserts blocks within the
          // same bubble. The bubble's content array is also stable per
          // turn (assistant turns never mutate), so collisions across
          // bubbles can't occur.
          key={`${role}-text-${index}-${hashText(block.text)}`}
          className="whitespace-pre-wrap break-words"
        >
          {block.text}
        </p>
      ))}
    </div>
  )
}
