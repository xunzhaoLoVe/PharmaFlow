import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Form, Input, Button, Card, message, Checkbox } from 'antd';
import { UserOutlined, LockOutlined, MedicineBoxOutlined } from '@ant-design/icons';
import { login } from '../api/auth';
import { useAuthStore } from '../store/authStore';

export default function LoginPage() {
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const authLogin = useAuthStore((s) => s.login);

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
    <div className="login-shell">
      <section className="login-panel">
        <div className="login-brand">
          <div className="login-brand-mark"><MedicineBoxOutlined /></div>
          <div>
            <h1>药品流向管理</h1>
            <p>PharmaFlow</p>
          </div>
        </div>
        <div className="login-copy">
          <h2>流向采集、审核、预警一体化工作台</h2>
          <p>面向药品渠道数据管理的日常运营界面。</p>
        </div>
        <div className="login-metrics">
          <span>数据采集</span>
          <span>流向审核</span>
          <span>串货预警</span>
        </div>
      </section>

      <Card className="login-card">
        <div className="login-card-head">
          <h2>登录系统</h2>
          <p>使用平台账号继续</p>
        </div>
        <Form onFinish={onFinish} size="large" initialValues={{ remember: false }}>
          <Form.Item name="username" rules={[{ required: true, message: '请输入用户名' }]}>
            <Input prefix={<UserOutlined />} placeholder="用户名" autoFocus />
          </Form.Item>
          <Form.Item name="password" rules={[{ required: true, message: '请输入密码' }]}>
            <Input.Password prefix={<LockOutlined />} placeholder="密码" />
          </Form.Item>
          <Form.Item name="remember" valuePropName="checked">
            <Checkbox>记住我（30天免登录）</Checkbox>
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit" loading={loading} block>
              登 录
            </Button>
          </Form.Item>
        </Form>
      </Card>
    </div>
  );
}
