/** Only in-app paths are allowed as a post-login destination, so a crafted link cannot redirect to another site. */
export function safeReturnUrl(url: string | null | undefined): string | null {
  if (!url || !url.startsWith('/') || url.startsWith('//') || url.includes('\\')) {
    return null;
  }

  return url;
}