import axios from "axios";
import { clearAuth, setAccessToken } from "@/features/auth/authSlice";
import { getClientStore } from "../redux/store";

const BASE_URL = "http://localhost:5263";

const instance = axios.create({
  baseURL: BASE_URL,
  timeout: 5000,
  withCredentials: true,
});

instance.interceptors.request.use((config) => {
  const token = getClientStore().getState().auth.accessToken;
  if (token) {
    config.headers.set("Authorization", `Bearer ${token}`);
  }
  return config;
});

// Aynı anda birden fazla 401 gelirse tek bir refresh isteği atılsın
let refreshing: Promise<string> | null = null;

const refreshAccessToken = () => {
  refreshing ??= axios
    // Ayrı axios çağrısı: refresh isteği bu interceptor'lara girmez, döngü olmaz
    .post<{ accessToken: string }>(`${BASE_URL}/account/refresh`, null, {
      withCredentials: true,
    })
    .then((r) => r.data.accessToken)
    .finally(() => {
      refreshing = null;
    });

  return refreshing;
};

instance.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config;

    if (error.response?.status === 401 && !original._retry) {
      original._retry = true;

      try {
        const newToken = await refreshAccessToken();
        getClientStore().dispatch(setAccessToken(newToken));

        original.headers.set("Authorization", `Bearer ${newToken}`);
        return instance(original);
      } catch {
        getClientStore().dispatch(clearAuth());
      }
    }

    return Promise.reject(error);
  }
);

export default instance;