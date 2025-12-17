export async function resolve(specifier, context, nextResolve) {
  if (specifier === '@identity-base/react-client') {
    return {
      url: new URL('./react-client-stub.mjs', import.meta.url).href,
      shortCircuit: true,
    }
  }

  return nextResolve(specifier, context)
}

