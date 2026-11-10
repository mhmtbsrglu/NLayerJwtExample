"use client";

import { useState } from "react";
import { Provider } from "react-redux";
import { getClientStore } from "../store";

export default function StoreProvider({ children }: { children: React.ReactNode }) {
  const [store] = useState(() => getClientStore());

  return <Provider store={store}>{children}</Provider>;
}