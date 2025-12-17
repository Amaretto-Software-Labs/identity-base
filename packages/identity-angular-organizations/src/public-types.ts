export type OrganizationHeaderRule = string | RegExp | ((url: string) => boolean)

export interface IdentityAngularOrganizationsConfig {
  apiBase: string
  timeoutMs?: number
  organizationHeader?: {
    headerName?: string
    include?: OrganizationHeaderRule[]
    exclude?: OrganizationHeaderRule[]
  }
}

