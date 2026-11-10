import { configureStore } from "@reduxjs/toolkit";
import authReducer from "@/features/auth/authSlice";

export const makeStore = () =>
  configureStore({
    reducer: {
      auth: authReducer,
    },
  });

export type AppStore = ReturnType<typeof makeStore>;
export type RootState = ReturnType<AppStore["getState"]>;
export type AppDispatch = AppStore["dispatch"];

// Tarayıcıda tek bir store, sunucuda her çağrıda yeni store.
// Böylece kullanıcılar arasında state sızıntısı olmaz.
let clientStore: AppStore | undefined;

export const getClientStore = (): AppStore => {
  if (typeof window === "undefined") return makeStore();
  if (!clientStore) clientStore = makeStore();
  return clientStore;
};