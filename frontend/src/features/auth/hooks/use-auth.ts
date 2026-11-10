"use client";

import { useCallback } from "react";
import { useAppDispatch, useAppSelector } from "@/lib/redux/hooks";
import { clearAuth, setAccessToken, setAuthLoading } from "@/features/auth/authSlice";
import instance from "@/lib/axios";

type LoginResponse = {
  token: string;
  expiration: string;
};

export function useAuth() {
  const dispatch = useAppDispatch();
  const accessToken = useAppSelector((state) => state.auth.accessToken);
  const status = useAppSelector((state) => state.auth.status);

  const login = useCallback(
    async (email: string, password: string) => {
      dispatch(setAuthLoading());
      try {
        const res = await instance.post<LoginResponse>("/accounts/login", {
          email,
          password,
        });
        dispatch(setAccessToken(res.data.token));
      } catch (err) {
        dispatch(clearAuth());
        throw err;
      }
    },
    [dispatch]
  );

  const register = useCallback(async (email: string, password: string) => {
    const res = await instance.post("/accounts/register", { email, password }).catch((x) => console.log(x));
    return res.data;
  }, []);

  const logout = useCallback(async () => {
    try {
      await instance.post("/accounts/logout");
    } finally {
      dispatch(clearAuth());
    }
  }, [dispatch]);

  return {
    accessToken,
    status,
    isAuthenticated: status === "authenticated",
    isLoading: status === "loading",
    login,
    register,
    logout,
  };
}