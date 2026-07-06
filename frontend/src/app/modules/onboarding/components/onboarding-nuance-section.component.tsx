import { useState } from 'react'
import { ChevronDownIcon } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Textarea } from '@/components/ui/textarea'
import { FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form'
import type {
  OnboardingFormControl,
  OnboardingStringFieldName,
} from '~/modules/onboarding/schemas/onboarding-form.schema'

export interface OnboardingNuanceSectionProps {
  control: OnboardingFormControl
  name: OnboardingStringFieldName
  /** Trigger copy, e.g. "Add detail". */
  label: string
  placeholder?: string
}

/**
 * The optional per-area free-text nuance box, behind an "Add detail" collapsible
 * (default closed) — the `MoreDetailsSection` pattern. The Textarea is always
 * part of the form (`shouldUnregister: false` on the form), so a filled-then-
 * collapsed note still submits; collapsing only hides the DOM. The runner's
 * nuance is written verbatim onto the topic record's free-text field and read by
 * later coaching prompts (FR-1.6) — there is no onboarding-time LLM extraction.
 */
export const OnboardingNuanceSection = ({
  control,
  name,
  label,
  placeholder,
}: OnboardingNuanceSectionProps) => {
  const [open, setOpen] = useState(false)

  return (
    <Collapsible open={open} onOpenChange={setOpen} className="flex flex-col gap-2">
      <CollapsibleTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="w-fit gap-1 px-2 text-muted-foreground"
          data-testid={`${name}-trigger`}
        >
          {label}
          <ChevronDownIcon
            aria-hidden="true"
            className={`size-4 transition-transform duration-200 ease-out motion-reduce:transition-none ${
              open ? 'rotate-180' : ''
            }`}
          />
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent className="data-[state=open]:animate-in data-[state=closed]:animate-out motion-reduce:animate-none">
        <FormField
          control={control}
          name={name}
          render={({ field }) => (
            <FormItem>
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
      </CollapsibleContent>
    </Collapsible>
  )
}
