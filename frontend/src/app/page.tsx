// src/app/page.tsx
"use client";

import { Button } from "@astryxdesign/core/Button";
import {
  Card,
  Divider,
  Heading,
  HStack,
  InputGroup,
  InputGroupText,
  Layout,
  LayoutContent,
  LayoutFooter,
  LayoutHeader,
  Switch,
  TextInput,
  VStack,
} from "@astryxdesign/core";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { IconMailQuestion, IconPassword, IconUserKey } from "@tabler/icons-react";
import { useAuth } from "@/features/auth/hooks/use-auth";

export default function Home() {
  const router = useRouter();
  const { login, register, isLoading } = useAuth();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [remember, setRemember] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState<"login" | "register" | null>(null);

  const isBusy = isLoading || submitting !== null;

  const handleLogin = async () => {
    setError(null);
    setSuccessMessage(null);
    setSubmitting("login");
    try {
      await login(email, password);
      router.push("/dashboard");
    } catch {
      setError("E-posta veya şifre hatalı.");
    } finally {
      setSubmitting(null);
    }
  };

  const handleRegister = async () => {
    setError(null);
    setSuccessMessage(null);
    setSubmitting("register");
    try {
      await register(email, password);
      setSuccessMessage("Kayıt başarılı. Onay bağlantısı e-postana gönderildi.");
    } catch {
      setError("Kayıt oluşturulamadı. Bilgilerini kontrol et.");
    } finally {
      setSubmitting(null);
    }
  };

  return (
    <div className="min-h-dvh flex items-center justify-center font-sans px-4 py-10">
      <main className="w-full max-w-sm flex flex-col items-center gap-6 sm:gap-8">
        <Card className="bg-violet-800! text-white text-2xl font-bold border-none! flex items-center justify-center size-20 sm:size-24">
          .NET
        </Card>

        <Card className="w-full">
          <Layout
            header={
              <LayoutHeader hasDivider>
                <HStack gap={3} className="items-center">
                  <IconUserKey />
                  <Heading level={3}>.NET Core Kimlik Doğrulama</Heading>
                </HStack>
              </LayoutHeader>
            }
            content={
              <LayoutContent>
                <VStack gap={4}>
                  <InputGroup label="E-posta">
                    <InputGroupText>
                      <IconMailQuestion />
                    </InputGroupText>
                    <TextInput
                      label="Email"
                      type="email"
                      description="Onay bağlantısı bu adrese gönderilecek"
                      value={email}
                      onChange={setEmail}
                      placeholder="ornek@mail.com"
                      isDisabled={isBusy}
                    />
                  </InputGroup>

                  <InputGroup label="Şifre">
                    <InputGroupText>
                      <IconPassword />
                    </InputGroupText>
                    <TextInput
                      label="Şifre"
                      type="password"
                      description="En az 8 karakter ve bir büyük harf"
                      value={password}
                      onChange={setPassword}
                      placeholder="Şifreni gir"
                      isDisabled={isBusy}
                    />
                  </InputGroup>

                  <Switch
                    label="Beni hatırla"
                    description="Bu cihazda oturumu açık tut"
                    value={remember}
                    onChange={setRemember}
                    isDisabled={isBusy}
                  />

                  {error && <p className="text-sm text-red-500">{error}</p>}
                  {successMessage && (
                    <p className="text-sm text-green-600">{successMessage}</p>
                  )}

                  <Button
                    label="Giriş Yap"
                    className="w-full"
                    variant="primary"
                    onClick={handleLogin}
                        isDisabled={isBusy}
                    isLoading={submitting === "register"}
                  >
                    Giriş Yap
                  </Button>

                  <Divider label="veya" />

                  <Button
                    label="Kayıt Ol"
                    className="w-full"
                    variant="secondary"
                    onClick={handleRegister}
                    isDisabled={isBusy}
                    isLoading={submitting === "register"}
                  >
                    Kayıt Ol
                  </Button>
                </VStack>
              </LayoutContent>
            }
            footer={<LayoutFooter />}
          />
        </Card>
      </main>
    </div>
  );
}