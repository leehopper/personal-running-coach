import type { ReactElement } from 'react'

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

// Role-based styling lives in a static map so consumers can tell at a
// glance which classes attach to which side of the conversation. Tailwind
// utilities only — no shadcn primitive is needed at this size.
const ROLE_STYLES: Record<MessageRole, string> = {
  user: 'ml-auto bg-slate-900 text-slate-50 rounded-2xl rounded-br-sm',
  assistant: 'mr-auto bg-slate-100 text-slate-900 rounded-2xl rounded-bl-sm',
}

export const MessageBubble = ({
  role,
  content,
  pending = false,
}: MessageBubbleProps): ReactElement => {
  // Ignore non-text blocks per spec § Unit 3 R03.6 — `thinking` / `tool_use`
  // / `tool_result` / `signature` must never be rendered as text. This
  // filter is the single guard for that contract.
  const textBlocks = content.filter(isTextBlock)

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
          // Stable key: text blocks within one bubble are positional and
          // immutable once received; `index` is safe here because the
          // bubble re-renders as a unit when content changes.
          key={`${role}-text-${index}`}
          className="whitespace-pre-wrap break-words"
        >
          {block.text}
        </p>
      ))}
    </div>
  )
}
