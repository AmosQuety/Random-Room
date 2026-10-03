import { ListEditor } from "../../components/ListEditor";
import type { SetupFormProps } from "../types";
import type { RandomPickerSetup } from "./types";

export function RandomPickerSetupForm({ value, onChange }: SetupFormProps<RandomPickerSetup>) {
  return (
    <ListEditor
      legend="Choices"
      hint="What the system will pick between."
      items={value}
      onChange={onChange}
      maxLength={80}
      placeholder={(i) => `Choice ${i + 1}`}
    />
  );
}
