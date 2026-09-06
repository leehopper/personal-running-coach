import { useState, type ReactNode } from 'react'
import { ChevronDownIcon } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Textarea } from '@/components/ui/textarea'
import { FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { MonoLabel } from '~/modules/common/components/mono-label/mono-label.component'
import type {
  OnboardingFormControl,
  OnboardingStringFieldName,
} from '~/modules/onboarding/schemas/onboarding-form.schema'

export interface OnboardingNuanceSectionProps {
  control: OnboardingFormControl
  name?: OnboardingStringFieldName
  /** Trigger copy, e.g. "Add detail". */
  label: string
  placeholder?: string
  /** Accessible label for the revealed textarea. */
  fieldLabel?: string
  /** Revealed content for disclosures with more than one field. */
  children?: ReactNode
  /** Optional stable trigger id for merged sections. */
  triggerTestId?: string
}

/**
 * The optional per-area free-text nuance box, behind an "Add detail" collapsible
 * (default closed) - the `MoreDetailsSection` pattern. The Textarea is always
 * part of the form (`shouldUnregister: false` on the form), so a filled-then-
 * collapsed note still submits; collapsing only hides the DOM. The runner's
 * nuance is written verbatim onto the topic record's free-text field and read by
 * later coaching prompts (FR-1.6) - there is no onboarding-time LLM extraction.
 */
export const OnboardingNuanceSection = ({
  control,
  name,
  label,
  placeholder,
  fieldLabel,
  children,
  triggerTestId,
}: OnboardingNuanceSectionProps) => {
  const [open, setOpen] = useState(false)
  const revealedContent =
    children ??
    (name !== undefined && placeholder !== undefined ? (
      <FormField
        control={control}
        name={name}
        render={({ field }) => (
          <FormItem>
            <FormLabel className="sr-only">{fieldLabel ?? label}</FormLabel>
            <FormControl>
              <Textarea
                rows={3}
                placeholder={placeholder}
                data-testid={`${name}-field`}
                {...field}
              />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
    ) : null)

  return (
    <Collapsible open={open} onOpenChange={setOpen} className="flex flex-col gap-2">
      <CollapsibleTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          className="t-data-label w-fit min-h-11 gap-1 px-2 text-clay-text active:scale-[0.98] motion-reduce:transition-none"
          data-testid={triggerTestId ?? (name === undefined ? undefined : `${name}-trigger`)}
        >
          <MonoLabel tone="clay">{label}</MonoLabel>
          <ChevronDownIcon
            aria-hidden="true"
            className={`size-4 transition-transform duration-200 ease-out motion-reduce:transition-none ${
              open ? 'rotate-180' : ''
            }`}
          />
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent className="overflow-hidden">{revealedContent}</CollapsibleContent>
    </Collapsible>
  )
}
