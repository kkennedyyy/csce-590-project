import type * as React from 'react';

declare global {
  namespace JSX {
    type Element = React.ReactElement;
    interface IntrinsicElements extends React.JSX.IntrinsicElements {}
  }
}

export {};
