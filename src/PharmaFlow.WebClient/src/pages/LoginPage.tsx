import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Form, Input, Button, message, Checkbox, Dropdown, Tooltip } from 'antd';
import {
  ApiOutlined,
  CheckOutlined,
  CloudSyncOutlined,
  DownOutlined,
  LockOutlined,
  LoginOutlined,
  MoonOutlined,
  SafetyCertificateOutlined,
  SunOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { login } from '../api/auth';
import { useAuthStore } from '../store/authStore';

export default function LoginPage() {
  const [loading, setLoading] = useState(false);
  const [language, setLanguage] = useState(() => localStorage.getItem('language') === 'en' ? 'en' : 'zh');
  const [darkMode, setDarkMode] = useState(() => localStorage.getItem('themeMode') === 'dark');
  const navigate = useNavigate();
  const authLogin = useAuthStore((s) => s.login);
  const languageMeta = language === 'zh' ? { country: 'CN', code: 'ZH' } : { country: 'US', code: 'EN' };
  const languageMenuItems = [
    {
      key: 'zh',
      label: (
        <span className="public-language-menu-item">
          <span>CN</span>
          <span>中文</span>
          {language === 'zh' && <CheckOutlined />}
        </span>
      ),
    },
    {
      key: 'en',
      label: (
        <span className="public-language-menu-item">
          <span>US</span>
          <span>English</span>
          {language === 'en' && <CheckOutlined />}
        </span>
      ),
    },
  ];

  const handleLanguageChange = ({ key }: { key: string }) => {
    const nextLanguage = key === 'en' ? 'en' : 'zh';
    setLanguage(nextLanguage);
    localStorage.setItem('language', nextLanguage);
  };

  const handleThemeMode = () => {
    const nextMode = !darkMode;
    setDarkMode(nextMode);
    localStorage.setItem('themeMode', nextMode ? 'dark' : 'light');
  };

  const onFinish = async (values: { username: string; password: string; remember: boolean }) => {
    setLoading(true);
    try {
      const result = await login(values);
      if (result.success) {
        authLogin(result.message!, {
          userId: result.userId!,
          displayName: result.displayName!,
          role: result.role!,
          permissions: result.permissions,
        });
        message.success('登录成功');
        navigate('/dashboard');
      } else {
        message.error(result.message || '登录失败');
      }
    } catch (e: any) {
      const msg = e?.response?.data?.message || e?.message || '登录失败，请检查用户名和密码';
      message.error(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={`login-shell ${darkMode ? 'is-public-dark' : ''}`}>
      <div className="login-topbar">
        <button type="button" className="public-brand-button" onClick={() => navigate('/')} aria-label="PharmaFlow">
          <span className="public-brand-mark" aria-hidden="true">
            <span />
            <span />
            <span />
          </span>
        </button>

        <div className="public-top-actions login-top-actions">
          <Dropdown
            menu={{ items: languageMenuItems, onClick: handleLanguageChange }}
            placement="bottomRight"
            trigger={['click']}
            overlayClassName="public-language-dropdown"
          >
            <button type="button" className="public-language-button" aria-label="切换语言">
              <span>{languageMeta.country}</span>
              <span>{languageMeta.code}</span>
              <DownOutlined />
            </button>
          </Dropdown>
          <span className="public-top-divider" />
          <Tooltip title={darkMode ? '浅色模式' : '深色模式'}>
            <button
              type="button"
              className={`public-icon-button ${darkMode ? 'is-active' : ''}`}
              onClick={handleThemeMode}
              aria-label={darkMode ? '浅色模式' : '深色模式'}
            >
              {darkMode ? <SunOutlined /> : <MoonOutlined />}
            </button>
          </Tooltip>
          <button type="button" className="public-login-button" onClick={() => navigate('/login')}>
            <LoginOutlined />
            登录
          </button>
        </div>
      </div>

      <main className="login-main">
        <section className="login-card">
          <div className="login-card-head">
            <h2>企业账号登录</h2>
            <p>请使用已授权账号进入药品流向数据工作台</p>
          </div>

          <Form className="login-form" layout="vertical" onFinish={onFinish} size="large" initialValues={{ remember: false }}>
            <Form.Item label="账号" name="username" rules={[{ required: true, message: '请输入用户名' }]}>
              <Input prefix={<UserOutlined />} placeholder="请输入账号" autoFocus />
            </Form.Item>
            <Form.Item label="密码" name="password" rules={[{ required: true, message: '请输入密码' }]}>
              <Input.Password prefix={<LockOutlined />} placeholder="请输入密码" />
            </Form.Item>

            <Form.Item className="login-form-row">
              <Form.Item name="remember" valuePropName="checked" noStyle>
                <Checkbox>记住我</Checkbox>
              </Form.Item>
              <button type="button" className="login-link-button">
                忘记密码？
              </button>
            </Form.Item>

            <Form.Item>
              <Button className="login-submit" type="primary" htmlType="submit" loading={loading} block icon={<LoginOutlined />}>
                登录
              </Button>
            </Form.Item>
          </Form>

          <div className="login-card-foot">
            <span>
              <SafetyCertificateOutlined /> 权限分级
            </span>
            <span>
              <CloudSyncOutlined /> 同步留痕
            </span>
            <span>
              <ApiOutlined /> 口径校验
            </span>
          </div>
        </section>
      </main>

      <div className="login-register">
        <span>账号开通或权限调整</span>
        <button type="button">请联系系统管理员</button>
      </div>

      <footer className="login-footer">© 2026 PharmaFlow. All rights reserved.</footer>
    </div>
  );
}
