/** Mirrors the API. The API checks the real file bytes, so these checks only save the user a round trip. */
export const MAX_FILES = 5;
export const MAX_BYTES = 5 * 1024 * 1024;
export const ACCEPTED_TYPES: readonly string[] = ['image/jpeg', 'image/png', 'image/webp'];
export const ACCEPT_ATTRIBUTE = ACCEPTED_TYPES.join(',');

export interface FileCheck {
  accepted: File[];
  problems: string[];
}

/** Splits picked files into the ones to upload and a message for each one that cannot be. */
export function validateFiles(files: readonly File[], existingCount: number): FileCheck {
  const accepted: File[] = [];
  const problems: string[] = [];
  let slots = Math.max(0, MAX_FILES - existingCount);
  let skipped = 0;

  for (const file of files) {
    if (!ACCEPTED_TYPES.includes(file.type)) {
      problems.push(`${file.name}: only JPEG, PNG and WebP photos are allowed.`);
      continue;
    }

    if (file.size === 0) {
      problems.push(`${file.name}: the file is empty.`);
      continue;
    }

    if (file.size > MAX_BYTES) {
      problems.push(`${file.name}: the photo is larger than ${MAX_BYTES / (1024 * 1024)} MB.`);
      continue;
    }

    if (slots === 0) {
      skipped++;
      continue;
    }

    accepted.push(file);
    slots--;
  }

  if (skipped > 0) {
    problems.push(
      `A request can have at most ${MAX_FILES} photos, so ${skipped} ${skipped === 1 ? 'photo was' : 'photos were'} not added.`,
    );
  }

  return { accepted, problems };
}

export function formatSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }

  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}