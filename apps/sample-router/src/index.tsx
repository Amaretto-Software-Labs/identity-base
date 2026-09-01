import {
  Children,
  createContext,
  isValidElement,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react'
import type {
  AnchorHTMLAttributes,
  MouseEvent,
  ReactElement,
  ReactNode,
} from 'react'

interface LocationState {
  pathname: string
  search: string
  hash: string
  state: unknown
}

interface NavigateOptions {
  replace?: boolean
  state?: unknown
}

type NavigateFunction = (to: string, options?: NavigateOptions) => void

interface RouterContextValue {
  location: LocationState
  navigate: NavigateFunction
}

interface RouteContextValue {
  basePath: string
  params: Readonly<Record<string, string>>
  outlet: ReactNode
}

const RouterContext = createContext<RouterContextValue | null>(null)
const RouteContext = createContext<RouteContextValue>({
  basePath: '/',
  params: {},
  outlet: null,
})

function readLocation(): LocationState {
  return {
    pathname: window.location.pathname,
    search: window.location.search,
    hash: window.location.hash,
    state: window.history.state?.identitySampleState ?? null,
  }
}

function resolveTarget(to: string, basePath = window.location.pathname): URL {
  if (to.startsWith('/') || /^[a-z][a-z\d+.-]*:/i.test(to)) {
    return new URL(to, window.location.origin)
  }

  const base = basePath.endsWith('/') ? basePath : `${basePath}/`
  return new URL(to, `${window.location.origin}${base}`)
}

export function BrowserRouter({ children }: { children: ReactNode }) {
  const [location, setLocation] = useState(readLocation)

  useEffect(() => {
    const onPopState = () => setLocation(readLocation())
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  const navigate = useCallback<NavigateFunction>((to: string, options: NavigateOptions = {}) => {
    const target = new URL(to, window.location.href)
    const state = { identitySampleState: options.state ?? null }
    if (options.replace) {
      window.history.replaceState(state, '', target)
    } else {
      window.history.pushState(state, '', target)
    }
    setLocation(readLocation())
  }, [])

  return (
    <RouterContext.Provider value={{ location, navigate }}>
      {children}
    </RouterContext.Provider>
  )
}

export interface RouteProps {
  path?: string
  index?: boolean
  element?: ReactElement
  children?: ReactNode
}

export function Route(_props: RouteProps) {
  return null
}

interface RouteMatch {
  element: ReactNode
  params: Record<string, string>
  basePath: string
}

function normalizeSegments(pathname: string): string[] {
  return pathname.split('/').filter(Boolean).map(segment => {
    try {
      return decodeURIComponent(segment)
    } catch {
      return segment
    }
  })
}

function joinBase(segments: string[], count: number): string {
  return count === 0 ? '/' : `/${segments.slice(0, count).map(encodeURIComponent).join('/')}`
}

function renderRoute(
  route: RouteProps,
  child: RouteMatch | null,
  params: Record<string, string>,
  basePath: string,
): RouteMatch {
  const outlet = child?.element ?? null
  const mergedParams = { ...params, ...child?.params }
  const element = route.element ?? outlet
  return {
    basePath: child?.basePath ?? basePath,
    params: mergedParams,
    element: route.element ? (
      <RouteContext.Provider value={{ basePath, params: mergedParams, outlet }}>
        {element}
      </RouteContext.Provider>
    ) : element,
  }
}

function matchRoutes(
  children: ReactNode,
  segments: string[],
  startIndex: number,
  inheritedParams: Record<string, string>,
): RouteMatch | null {
  for (const childElement of Children.toArray(children)) {
    if (!isValidElement<RouteProps>(childElement) || childElement.type !== Route) {
      continue
    }

    const route = childElement.props
    if (route.index) {
      if (startIndex === segments.length) {
        return renderRoute(route, null, inheritedParams, joinBase(segments, startIndex))
      }
      continue
    }

    if (!route.path) {
      const nested = matchRoutes(route.children, segments, startIndex, inheritedParams)
      if (nested) {
        return renderRoute(route, nested, inheritedParams, joinBase(segments, startIndex))
      }
      continue
    }

    if (route.path === '*') {
      return renderRoute(route, null, inheritedParams, joinBase(segments, startIndex))
    }

    const absolute = route.path.startsWith('/')
    const routeSegments = route.path.split('/').filter(Boolean)
    const routeStart = absolute ? 0 : startIndex
    if (routeStart + routeSegments.length > segments.length) {
      continue
    }

    const params = { ...inheritedParams }
    let matched = true
    for (let offset = 0; offset < routeSegments.length; offset += 1) {
      const expected = routeSegments[offset]
      const actual = segments[routeStart + offset]
      if (expected?.startsWith(':') && actual !== undefined) {
        params[expected.slice(1)] = actual
      } else if (expected !== actual) {
        matched = false
        break
      }
    }
    if (!matched) {
      continue
    }

    const nextIndex = routeStart + routeSegments.length
    const basePath = joinBase(segments, nextIndex)
    const nested = matchRoutes(route.children, segments, nextIndex, params)
    if (nested) {
      return renderRoute(route, nested, params, basePath)
    }
    if (nextIndex === segments.length) {
      return renderRoute(route, null, params, basePath)
    }
  }

  return null
}

export function Routes({ children }: { children: ReactNode }) {
  const { location } = useRouter()
  const match = useMemo(
    () => matchRoutes(children, normalizeSegments(location.pathname), 0, {}),
    [children, location.pathname],
  )
  return match?.element ?? null
}

export function Outlet() {
  return useContext(RouteContext).outlet
}

export function useLocation(): LocationState {
  return useRouter().location
}

export function useNavigate(): NavigateFunction {
  const { navigate } = useRouter()
  const { basePath } = useContext(RouteContext)
  return useCallback((to: string, options?: NavigateOptions) => {
    const target = resolveTarget(to, basePath)
    navigate(`${target.pathname}${target.search}${target.hash}`, options)
  }, [basePath, navigate])
}

type Params<Key extends string> = {
  readonly [key in Key]: string | undefined
}

export function useParams<
  ParamsOrKey extends string | Record<string, string | undefined> = string,
>(): Readonly<
  [ParamsOrKey] extends [string]
    ? Params<ParamsOrKey>
    : Partial<ParamsOrKey>
> {
  return useContext(RouteContext).params as Readonly<
    [ParamsOrKey] extends [string]
      ? Params<ParamsOrKey>
      : Partial<ParamsOrKey>
  >
}

export function useSearchParams(): [
  URLSearchParams,
  (next: URLSearchParams | Record<string, string>, options?: NavigateOptions) => void,
] {
  const location = useLocation()
  const navigate = useNavigate()
  const params = useMemo(() => new URLSearchParams(location.search), [location.search])
  const setParams = useCallback((
    next: URLSearchParams | Record<string, string>,
    options?: NavigateOptions,
  ) => {
    const search = next instanceof URLSearchParams
      ? next
      : new URLSearchParams(next)
    navigate(`${location.pathname}?${search.toString()}`, options)
  }, [location.pathname, navigate])
  return [params, setParams]
}

export function Navigate({ to, replace, state }: { to: string; replace?: boolean; state?: unknown }) {
  const navigate = useNavigate()
  useEffect(() => navigate(to, { replace, state }), [navigate, replace, state, to])
  return null
}

interface LinkProps extends Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'href'> {
  to: string
  state?: unknown
  replace?: boolean
}

function shouldHandleClick(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && (!event.currentTarget.target || event.currentTarget.target === '_self')
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey
}

export function Link({ to, state, replace, onClick, ...props }: LinkProps) {
  const navigate = useNavigate()
  const { basePath } = useContext(RouteContext)
  const target = resolveTarget(to, basePath)
  const sameOrigin = target.origin === window.location.origin
  const href = sameOrigin
    ? `${target.pathname}${target.search}${target.hash}`
    : target.href
  return (
    <a
      {...props}
      href={href}
      onClick={event => {
        onClick?.(event)
        if (!sameOrigin || !shouldHandleClick(event)) return
        event.preventDefault()
        navigate(to, { state, replace })
      }}
    />
  )
}

interface NavLinkProps extends Omit<LinkProps, 'className'> {
  className?: string | ((state: { isActive: boolean }) => string)
  end?: boolean
}

export function NavLink({ className, end = false, to, ...props }: NavLinkProps) {
  const { pathname } = useLocation()
  const { basePath } = useContext(RouteContext)
  const target = resolveTarget(to, basePath).pathname
  const isActive = end
    ? pathname === target
    : pathname === target || pathname.startsWith(`${target}/`)
  return (
    <Link
      {...props}
      to={to}
      aria-current={isActive ? 'page' : undefined}
      className={typeof className === 'function' ? className({ isActive }) : className}
    />
  )
}

function useRouter(): RouterContextValue {
  const router = useContext(RouterContext)
  if (!router) {
    throw new Error('Sample router components must be rendered inside BrowserRouter.')
  }
  return router
}
