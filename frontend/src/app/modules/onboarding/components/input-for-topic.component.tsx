import type { ReactElement } from 'react'
import { InputForTopicMap } from './input-for-topic.helpers'
import type { InputForTopicProps } from './input-for-topic.types'

/**
 * Dispatcher that picks the right per-topic input component based on the
 * server-supplied `suggestedInputType`. The dispatch surface is a static
 * `Record` (not a switch) so consumers can swap one entry without forking
 * the component (e.g., Slice 4 substitutes a chat-style text input). The
 * map and shared type interfaces live in sibling files
 * (`input-for-topic.helpers.ts` / `input-for-topic.types.ts`) so this file
 * exports React components only — keeps Vite's react-refresh happy.
 */
export const InputForTopic = ({
  suggestedInputType,
  ...rest
}: InputForTopicProps): ReactElement => {
  const Component = InputForTopicMap[suggestedInputType]
  return <Component {...rest} />
}
