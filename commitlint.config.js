export default {
  extends: ['@commitlint/config-conventional'],
  // Dependabot writes its own subjects ("Bump ...") and long release-note links in the body.
  ignores: [(message) => /^(build|ci)\(deps(-dev)?\): [Bb]ump /.test(message)],
};
