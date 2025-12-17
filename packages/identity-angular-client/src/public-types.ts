import type { IdentityConfig } from '@identity-base/client-core'

export type IdentityTokenAttachmentRule = string | RegExp | ((url: string) => boolean)

export interface IdentityAngularClientConfig extends IdentityConfig {
  tokenAttachment?: {
    include?: IdentityTokenAttachmentRule[]
    exclude?: IdentityTokenAttachmentRule[]
  }
}

