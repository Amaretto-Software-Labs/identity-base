import { Injectable } from '@angular/core'
import { BehaviorSubject } from 'rxjs'

@Injectable()
export class ActiveOrganizationService {
  private readonly organizationIdSubject = new BehaviorSubject<string | null>(null)
  readonly organizationId$ = this.organizationIdSubject.asObservable()

  get organizationId(): string | null {
    return this.organizationIdSubject.getValue()
  }

  setOrganizationId(organizationId: string | null): void {
    this.organizationIdSubject.next(organizationId)
  }

  clear(): void {
    this.organizationIdSubject.next(null)
  }
}

