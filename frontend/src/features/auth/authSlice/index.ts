import { createSlice, PayloadAction } from "@reduxjs/toolkit";

interface AuthState {
  accessToken: string | null;
  status: "idle" | "loading" | "authenticated" | "unauthenticated";
}

const initialState: AuthState = {
  accessToken: null,
  status: "idle",
};

const authSlice = createSlice({
  name: "auth",
  initialState,
  reducers: {
    setAccessToken(state, action: PayloadAction<string>) {
      state.accessToken = action.payload;
      state.status = "authenticated";
    },
    clearAuth(state) {
      state.accessToken = null;
      state.status = "unauthenticated";
    },
    setAuthLoading(state) {
      state.status = "loading";
    },
  },
});

export const { setAccessToken, clearAuth, setAuthLoading } = authSlice.actions;
export default authSlice.reducer;